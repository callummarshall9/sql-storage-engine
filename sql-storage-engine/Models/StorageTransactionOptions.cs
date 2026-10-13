// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models;
/// <summary>Data describing one transaction's timeout, isolation and journal quota.</summary>
public sealed class StorageTransactionOptions
{
    public const long MaximumSupportedJournalBytes = 256L * 1024 * 1024;
    public TimeSpan Timeout { get; init; }
    public StorageTransactionIsolation Isolation { get; init; }
    public long MaximumJournalBytes { get; init; }
}