// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Storage;
internal sealed record StorageQueryContext
{
    public string? Operation { get; init; }
    public IEnumerable<SqlValue>? Values { get; init; }
    public IEnumerable<ColumnId>? UnmaskedColumns { get; init; }
    public bool Inclusive { get; init; }
    public bool CanUnmaskAll { get; init; }
    public long? RepeatableSeed { get; init; }
    public decimal Percentage { get; init; }
    public long RowCount { get; init; }
    public DateTime Instant { get; init; }
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public StorageIndexBound? BoundResult { get; init; }
    public StorageReadOptions? ReadOptionsResult { get; init; }
    public StorageEngineOptions? EngineOptionsResult { get; init; }
    public StorageTableSample? SampleResult { get; init; }
    public StorageTemporalQuery? TemporalResult { get; init; }
}