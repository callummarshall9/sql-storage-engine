// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Services.Foundations.StorageQueries;

namespace sql_storage_engine.Exposures.Storage;
public static class StorageQueryExposure
{
    private static readonly IStorageQueryService service = new StorageQueryService();
    public static StorageIndexBound CreateStorageIndexBound(System.Collections.Generic.IEnumerable<sql_storage_engine.Models.Rows.SqlValue> values, bool inclusive = true) =>
        service.CreateStorageQueryContext(storageQueryContext: new StorageQueryContext { Operation = "Bound", Values = values, Inclusive = inclusive })
            .BoundResult!;

    public static StorageReadOptions CreateStorageReadOptions(bool canUnmaskAll = false, System.Collections.Generic.IEnumerable<sql_storage_engine.Models.Identifiers.ColumnId>? unmaskedColumns = null) =>
        service.CreateStorageQueryContext(storageQueryContext: new StorageQueryContext { Operation = "Read", CanUnmaskAll = canUnmaskAll, UnmaskedColumns = unmaskedColumns })
            .ReadOptionsResult!;

    public static StorageEngineOptions CreateStorageEngineOptions() =>
        service.CreateStorageQueryContext(storageQueryContext: new StorageQueryContext { Operation = "Engine" })
            .EngineOptionsResult!;

    public static StoragePercentTableSample CreateStoragePercentTableSample(decimal percentage, long? repeatableSeed = null) =>
        (StoragePercentTableSample)service.CreateStorageQueryContext(storageQueryContext: new StorageQueryContext { Operation = "Percent", Percentage = percentage, RepeatableSeed = repeatableSeed })
            .SampleResult!;

    public static StorageRowsTableSample CreateStorageRowsTableSample(long rowCount, long? repeatableSeed = null) =>
        (StorageRowsTableSample)service.CreateStorageQueryContext(storageQueryContext: new StorageQueryContext { Operation = "Rows", RowCount = rowCount, RepeatableSeed = repeatableSeed })
            .SampleResult!;

    public static StorageTemporalAsOf CreateStorageTemporalAsOf(System.DateTime instant) =>
        (StorageTemporalAsOf)service.CreateStorageQueryContext(storageQueryContext: new StorageQueryContext { Operation = "AsOf", Instant = instant })
            .TemporalResult!;

    public static StorageTemporalFromTo CreateStorageTemporalFromTo(System.DateTime start, System.DateTime end) =>
        (StorageTemporalFromTo)service.CreateStorageQueryContext(storageQueryContext: new StorageQueryContext { Operation = "FromTo", Start = start, End = end })
            .TemporalResult!;

    public static StorageTemporalBetweenAnd CreateStorageTemporalBetweenAnd(System.DateTime start, System.DateTime end) =>
        (StorageTemporalBetweenAnd)service.CreateStorageQueryContext(storageQueryContext: new StorageQueryContext { Operation = "BetweenAnd", Start = start, End = end })
            .TemporalResult!;

    public static StorageTemporalContainedIn CreateStorageTemporalContainedIn(System.DateTime start, System.DateTime end) =>
        (StorageTemporalContainedIn)service.CreateStorageQueryContext(storageQueryContext: new StorageQueryContext { Operation = "ContainedIn", Start = start, End = end })
            .TemporalResult!;

    public static StorageTemporalAll CreateStorageTemporalAll() =>
        (StorageTemporalAll)service.CreateStorageQueryContext(storageQueryContext: new StorageQueryContext { Operation = "All" })
            .TemporalResult!;
}