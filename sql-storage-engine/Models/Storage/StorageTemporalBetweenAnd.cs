// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Storage;
public sealed record StorageTemporalBetweenAnd : StorageTemporalQuery
{
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
}