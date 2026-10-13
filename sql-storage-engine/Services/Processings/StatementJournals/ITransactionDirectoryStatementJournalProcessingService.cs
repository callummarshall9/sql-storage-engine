// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Processings.StatementJournals;
internal interface ITransactionDirectoryStatementJournalProcessingService
{
    string ReadStatementJournalStatePath(string databasePath);

    ValueTask<StatementJournalState> CreateStatementJournalStateAsync(string databasePath, CancellationToken cancellationToken, string? explicitPath = null);

    ValueTask CommitStatementJournalStateAsync(StatementJournalState statementJournalState, CancellationToken cancellationToken, bool retain = false);

    ValueTask RestoreStatementJournalStateAsync(StatementJournalState statementJournalState, int pageSize, Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask> writePage, Action<long> restoreLength, Func<CancellationToken, ValueTask> flush, Func<CancellationToken, ValueTask> reloadHeader, CancellationToken cancellationToken, bool retain = false);

    ValueTask RecoverStatementJournalStateAsync(string databasePath, CancellationToken cancellationToken, string? explicitPath = null, bool retain = false);

    ValueTask<(byte State, byte[] Bytes)> ReadStatementJournalStateSnapshotAsync(string path, CancellationToken cancellationToken);

    void RemoveStatementJournalState(StatementJournalState statementJournalState);

    ValueTask ReleaseStatementJournalStateAsync(StatementJournalState statementJournalState);

    ValueTask WriteStatementJournalStateMetadataAsync<T>(string path, T value, CancellationToken cancellationToken);

    ValueTask<(Guid Value, Guid DatabaseId)> ReadStatementJournalStateIdentityAsync(string path, CancellationToken cancellationToken);

    bool CheckStatementJournalStateFile(string path);

    void DeleteStatementJournalStateFile(string path);

    long ReadStatementJournalStateFileLength(string path);

    ValueTask RestoreStatementJournalStateOwnerAsync(StatementJournalState statementJournalState, Func<StatementJournalState, CancellationToken, ValueTask> restore);
}