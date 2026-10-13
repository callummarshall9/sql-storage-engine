// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Processings.StatementJournals;
internal sealed partial class TransactionDirectoryStatementJournalProcessingService
{
    private static void ValidateDatabasePath(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: databasePath);
    }

    private static void ValidateJournalPaths(string databasePath, CancellationToken cancellationToken, string? explicitPath)
    {
        ValidateDatabasePath(databasePath: databasePath);

        if (explicitPath != null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(argument: explicitPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateStatementJournalState(StatementJournalState statementJournalState)
    {
        ArgumentNullException.ThrowIfNull(argument: statementJournalState);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: statementJournalState.Path);
    }

    private static void ValidateJournalOperation(StatementJournalState statementJournalState, CancellationToken cancellationToken, bool retain)
    {
        ValidateStatementJournalState(statementJournalState: statementJournalState);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateRecovery(string databasePath, CancellationToken cancellationToken, string? explicitPath, bool retain)
    {
        ValidateJournalPaths(databasePath: databasePath, cancellationToken: cancellationToken, explicitPath: explicitPath);
    }

    private static void ValidateSnapshotPath(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateRestore(StatementJournalState statementJournalState, int pageSize, Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask> writePage, Action<long> restoreLength, Func<CancellationToken, ValueTask> flush, Func<CancellationToken, ValueTask> reloadHeader, CancellationToken cancellationToken, bool retain)
    {
        ValidateJournalOperation(statementJournalState: statementJournalState, cancellationToken: cancellationToken, retain: retain);

        if (!Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)pageSize))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(pageSize));
        }

        ArgumentNullException.ThrowIfNull(argument: writePage);
        ArgumentNullException.ThrowIfNull(argument: restoreLength);
        ArgumentNullException.ThrowIfNull(argument: flush);
        ArgumentNullException.ThrowIfNull(argument: reloadHeader);
    }

    private static void ValidateStatementJournalMetadata<T>(string path, T value, CancellationToken cancellationToken)
    {
        ValidateSnapshotPath(path: path, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: value);
    }

    private static void ValidateJournalFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);
    }

    private static void ValidateStatementJournalStateFileOnDelete(string path)
    {
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: path), path is null, nameof(path))]);
    }

    private static void Validate((bool Invalid, bool Missing, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (!input.Invalid)
            {
                continue;
            }

            if (input.Missing)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }

            throw new ArgumentException(message: "A transaction-file path is required.", paramName: input.ParameterName);
        }
    }

    private static void ValidateOwnerRestore(StatementJournalState statementJournalState, Func<StatementJournalState, CancellationToken, ValueTask> restore)
    {
        ValidateStatementJournalState(statementJournalState: statementJournalState);
        ArgumentNullException.ThrowIfNull(argument: restore);
    }
}