// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Services.Processings.StatementJournals;
using sql_storage_engine.Services.Foundations.TransactionDirectories;
using sql_storage_engine.Brokers.Directories;

namespace sql_storage_engine.Exposures.Transactions;
internal sealed class StatementJournalExposure : IAsyncDisposable
{
    private static readonly ITransactionDirectoryStatementJournalProcessingService service = new TransactionDirectoryStatementJournalProcessingService(transactionDirectoryService: new TransactionDirectoryService(transactionDirectoryBroker: new TransactionDirectoryBroker()));
    private readonly StatementJournalState state;
    private StatementJournalExposure(StatementJournalState statementJournalState)
    {
        state = statementJournalState;
    }

    public static string GetPath(string databasePath) =>
        service.ReadStatementJournalStatePath(databasePath: databasePath);

    public static async ValueTask<StatementJournalExposure> CreateStatementJournalExposureAsync(string databasePath, CancellationToken cancellationToken, string? explicitPath = null)
    {
        var model = await service.CreateStatementJournalStateAsync(databasePath: databasePath, cancellationToken: cancellationToken, explicitPath: explicitPath)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new StatementJournalExposure(statementJournalState: model);
    }

    public ValueTask MarkCommittedAsync(CancellationToken cancellationToken, bool retain = false) =>
        service.CommitStatementJournalStateAsync(statementJournalState: state, cancellationToken: cancellationToken, retain: retain);

    public ValueTask RestoreAsync(int pageSize, Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask> writePage, Action<long> restoreLength, Func<CancellationToken, ValueTask> flush, Func<CancellationToken, ValueTask> reloadHeader, CancellationToken cancellationToken, bool retain = false) =>
        service.RestoreStatementJournalStateAsync(statementJournalState: state, pageSize: pageSize, writePage: writePage, restoreLength: restoreLength, flush: flush, reloadHeader: reloadHeader, cancellationToken: cancellationToken, retain: retain);

    public static ValueTask RecoverIfNeededAsync(string databasePath, CancellationToken cancellationToken, string? explicitPath = null, bool retain = false) =>
        service.RecoverStatementJournalStateAsync(databasePath: databasePath, cancellationToken: cancellationToken, explicitPath: explicitPath, retain: retain);

    public static ValueTask<(byte State, byte[] Bytes)> ReadSnapshotAsync(string path, CancellationToken token) =>
        service.ReadStatementJournalStateSnapshotAsync(path: path, cancellationToken: token);

    public void Delete() =>
        service.RemoveStatementJournalState(statementJournalState: state);

    public ValueTask DisposeAsync() =>
        service.ReleaseStatementJournalStateAsync(statementJournalState: state);

    public static StatementJournalExposure BindStatementJournalExposure(StatementJournalState statementJournalState) =>
        new(statementJournalState: statementJournalState);
}