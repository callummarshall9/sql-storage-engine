// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Diagnostics;
public sealed record CompatibilityWalRecord
{
    public ulong Lsn { get; init; }
    public ulong TransactionId { get; init; }
    public string Type { get => _type!; init => _type = value; }

    private string? _type;
}