// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Services.Processings.StatementJournals;
using sql_storage_engine.Services.Processings.SavepointFiles;

namespace sql_storage_engine.Services.Orchestrations.SavepointSessions;
internal sealed partial class StorageSavepointSessionOrchestrationService(ITransactionDirectoryStatementJournalProcessingService journals, ITransactionDirectorySavepointProcessingService savepointFiles) : IStorageSavepointSessionOrchestrationService
{
    public StorageSavepointSession PrepareStorageSavepointSession(StorageSavepointSession storageSavepointSession) =>
        TryCatch(operation: () =>
    {
        ValidateSession(storageSavepointSession: storageSavepointSession);
        return storageSavepointSession;
    });

    public ValueTask<StorageSavepointSession> CreateStorageSavepointSessionAsync(StorageSavepointSession storageSavepointSession, string name, CancellationToken token) =>
        TryCatch(operation: async () =>
    {
        ValidateSessionName(storageSavepointSession: storageSavepointSession, name: name, token: token);
        token.ThrowIfCancellationRequested();

        if (storageSavepointSession.Runtime.Entries.Count == StorageSavepoint.MaximumCount)
        {
            throw new InvalidOperationException("Savepoint count quota exceeded.");
        }

        await storageSavepointSession.Flush(arg: token)
            .ConfigureAwait(continueOnCapturedContext: false);

        var bytes = checked(journals.ReadStatementJournalStateFileLength(path: storageSavepointSession.DatabasePath) + 64);

        if (bytes > storageSavepointSession.MaximumBytes - storageSavepointSession.Runtime.Bytes)
        {
            throw new InvalidOperationException("Savepoint image quota exceeded.");
        }

        storageSavepointSession.Observer?.Invoke(obj: "BeforeSavepointCreate");

        var journal = await CreateStatementJournalStateImageAsync(storageSavepointSession: storageSavepointSession, token: token)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            storageSavepointSession.Observer?.Invoke(obj: "AfterSavepointCreate");
            token.ThrowIfCancellationRequested();

            var point = new StorageSavepoint
            {
                Transaction = storageSavepointSession.Identity,
                Value = Guid.NewGuid(),
                Name = name
            };

            storageSavepointSession.Runtime.Entries.Add(item: new sql_storage_engine.Models.Transactions.StorageSavepointEntry { Token = point, Journal = journal, Bytes = bytes });
            storageSavepointSession.Runtime.Bytes += bytes;

            return storageSavepointSession with
            {
                Result = point
            };
        }
        catch
        {
            DeleteImage(journal: journal);
            throw;
        }
    });

    private async ValueTask<StatementJournalState> CreateStatementJournalStateImageAsync(StorageSavepointSession storageSavepointSession, CancellationToken token)
    {
        var path = savepointFiles.GenerateSavepointPath(databasePath: storageSavepointSession.DatabasePath);

        try
        {
            return await journals.CreateStatementJournalStateAsync(databasePath: storageSavepointSession.DatabasePath, cancellationToken: token, explicitPath: path)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception error)
        {
            try
            {
                journals.DeleteStatementJournalStateFile(path: path);
            }
            catch (Exception cleanup)
            {
                throw new AggregateException("Savepoint image cleanup failed.", error, cleanup);
            }

            throw;
        }
    }

    private void DeleteImage(StatementJournalState journal)
    {
        try
        {
            journals.RemoveStatementJournalState(statementJournalState: journal);
        }
        catch (Exception error)
        {
            throw new AggregateException("Savepoint image cleanup failed; root rollback is required.", error);
        }
    }

    public StorageSavepointSession FindStorageSavepointSession(StorageSavepointSession storageSavepointSession, string name) =>
        TryCatch(operation: () =>
    {
        ValidateSessionName(storageSavepointSession: storageSavepointSession, name: name, token: CancellationToken.None);

        return storageSavepointSession with
        {
            Result = storageSavepointSession.Runtime.Entries.LastOrDefault(predicate: entry => StringComparer.Ordinal.Equals(x: entry.Token.Name, y: name))?.Token ?? throw new ArgumentException("Unknown savepoint name.", nameof(name))
        };
    });

    public ValueTask RollbackStorageSavepointSessionAsync(StorageSavepointSession storageSavepointSession, CancellationToken token) =>
        TryCatch(operation: async () =>
    {
        ValidateRollback(storageSavepointSession: storageSavepointSession, token: token);
        var point = storageSavepointSession.RequestedPoint!;
        var index = storageSavepointSession.Runtime.Entries.FindIndex(match: entry => entry.Token == point);

        if (point.Transaction != storageSavepointSession.Identity || index < 0)
        {
            throw new ArgumentException("Unknown, foreign or invalidated savepoint.", nameof(point));
        }

        token.ThrowIfCancellationRequested();

        await storageSavepointSession.Flush(arg: token)
            .ConfigureAwait(continueOnCapturedContext: false);

        var guard = await CreateStatementJournalStateImageAsync(storageSavepointSession: storageSavepointSession, token: token)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            storageSavepointSession.Observer?.Invoke(obj: "BeforeSavepointRestore");
            token.ThrowIfCancellationRequested();

            try
            {
                await journals.RestoreStatementJournalStateOwnerAsync(statementJournalState: storageSavepointSession.Runtime.Entries[index].Journal, restore: storageSavepointSession.Restore)
            .ConfigureAwait(continueOnCapturedContext: false);

                storageSavepointSession.Observer?.Invoke(obj: "AfterSavepointRestore");
            }
            catch (Exception restoreError)
            {
                try
                {
                    storageSavepointSession.Observer?.Invoke(obj: "BeforeSavepointRecovery");

                    await journals.RestoreStatementJournalStateOwnerAsync(statementJournalState: guard, restore: storageSavepointSession.Restore)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (Exception recoveryError)
                {
                    throw new AggregateException("Savepoint recovery failed; root rollback is required.", restoreError, recoveryError);
                }

                throw new sql_storage_engine.Models.Exceptions.TransactionRecoveryEvidenceException(message: "Savepoint restoration failed; the pre-operation image was recovered.", innerException: restoreError);
            }

            var invalidated = storageSavepointSession.Runtime.Entries.Skip(count: index + 1)
            .ToArray();

            storageSavepointSession.Runtime.Entries.RemoveRange(index: index + 1, count: storageSavepointSession.Runtime.Entries.Count - index - 1);

            foreach (var entry in invalidated)
            {
                storageSavepointSession.Runtime.Bytes -= entry.Bytes;
                DeleteImage(journal: entry.Journal);
            }
        }
        finally
        {
            DeleteImage(journal: guard);
        }
    });
}