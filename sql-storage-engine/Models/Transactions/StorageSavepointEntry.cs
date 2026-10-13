// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Transactions;
internal sealed record StorageSavepointEntry
{
    public StorageSavepoint Token { get => _token!; init => _token = value; }
    public StatementJournalState Journal { get => _journal!; init => _journal = value; }

    private StorageSavepoint? _token;
    private StatementJournalState? _journal;
    public long Bytes { get; init; }
}