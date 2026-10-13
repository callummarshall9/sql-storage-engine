// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;
using sql_storage_engine.Logging;

namespace sql_storage_engine.Models.Logging;
public sealed record WalRecord
{
    public LogSequenceNumber Lsn { get; init; }
    public LogSequenceNumber PreviousLsn { get; init; }
    public TransactionId TransactionId { get; init; }
    public WalRecordType Type { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; }
}