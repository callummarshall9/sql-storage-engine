namespace sql_storage_engine.Transactions;

internal sealed record StorageSavepointEntry(StorageSavepoint Token, StatementJournal Journal, long Bytes);
