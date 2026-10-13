// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Transactions;
internal sealed record StorageSavepointSession
{
    public string DatabasePath { get => databasePath!; init => databasePath = value; }

    private string? databasePath;
    public StorageTransactionIdentity Identity { get; init; }
    public long MaximumBytes { get; init; }
    public SavepointRuntimeState Runtime { get => runtime!; init => runtime = value; }
    public Func<CancellationToken, ValueTask> Flush { get => flush!; init => flush = value; }
    public Func<StatementJournalState, CancellationToken, ValueTask> Restore { get => restore!; init => restore = value; }

    private SavepointRuntimeState? runtime;
    private Func<CancellationToken, ValueTask>? flush;
    private Func<StatementJournalState, CancellationToken, ValueTask>? restore;
    public Action<string>? Observer { get; init; }
    public StorageSavepoint? RequestedPoint { get; init; }
    public StorageSavepoint? Result { get; init; }
}