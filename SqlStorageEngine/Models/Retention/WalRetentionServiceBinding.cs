// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Retention;
public sealed class WalRetentionServiceBinding
{
    public object? Source { get; init; }
    public ulong? RegistrationId { get; init; }
}