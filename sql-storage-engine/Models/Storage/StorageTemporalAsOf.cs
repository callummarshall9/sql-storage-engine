// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Storage;
public sealed record StorageTemporalAsOf : StorageTemporalQuery
{
    public DateTime Instant { get; init; }
}