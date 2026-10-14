// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Strategies;
public sealed record StorageCommitStatus
{
    public long CommittedGeneration { get; init; }
    public int PendingPages { get; init; }
    public string? LastError { get; init; }
}