// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using System.Collections.Frozen;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Foundations.StorageQueries;
internal sealed partial class StorageQueryService : IStorageQueryService
{
    public StorageQueryContext CreateStorageQueryContext(StorageQueryContext storageQueryContext) =>
        TryCatch(operation: () =>
    {
        ValidateStorageQueryContext(storageQueryContext: storageQueryContext);
        var request = storageQueryContext;

        switch (request.Operation)
        {
            case "Bound":
                var values = request.Values!.ToArray();
                Validate(inputs: [(values.Any(predicate: value => value is null), nameof(StorageQueryContext.Values))]);
                return request with
                {
                    BoundResult = new StorageIndexBound
                    {
                        Values = Array.AsReadOnly(array: values),
                        Inclusive = request.Inclusive
                    }
                };
            case "Read":
                return request with
                {
                    ReadOptionsResult = new StorageReadOptions
                    {
                        CanUnmaskAll = request.CanUnmaskAll,
                        UnmaskedColumns = (request.UnmaskedColumns ?? []).ToFrozenSet()
                    }
                };
            case "Engine":
                return request with
                {
                    EngineOptionsResult = new StorageEngineOptions
                    {
                        PageSize = sql_storage_engine.Models.Pages.PageConstants.DefaultSize,
                        BufferPoolCapacity = 256,
                        InlineValueThreshold = 1024,
                        DefaultCollation = "SQL_Latin1_General_CP1_CI_AS",
                        TimeProvider = TimeProvider.System
                    }
                };
            case "Percent":
                return request with
                {
                    SampleResult = new StoragePercentTableSample
                    {
                        Percentage = request.Percentage,
                        RepeatableSeed = request.RepeatableSeed
                    }
                };
            case "Rows":
                return request with
                {
                    SampleResult = new StorageRowsTableSample
                    {
                        RowCount = request.RowCount,
                        RepeatableSeed = request.RepeatableSeed
                    }
                };
            case "AsOf":
                return request with
                {
                    TemporalResult = new StorageTemporalAsOf
                    {
                        Instant = NormalizePublicQueryTimestamp(timestamp: request.Instant)
                    }
                };
            case "FromTo":
                return request with
                {
                    TemporalResult = new StorageTemporalFromTo
                    {
                        Start = NormalizePublicQueryTimestamp(timestamp: request.Start),
                        End = NormalizePublicQueryTimestamp(timestamp: request.End)
                    }
                };
            case "BetweenAnd":
                return request with
                {
                    TemporalResult = new StorageTemporalBetweenAnd
                    {
                        Start = NormalizePublicQueryTimestamp(timestamp: request.Start),
                        End = NormalizePublicQueryTimestamp(timestamp: request.End)
                    }
                };
            case "ContainedIn":
                return request with
                {
                    TemporalResult = new StorageTemporalContainedIn
                    {
                        Start = NormalizePublicQueryTimestamp(timestamp: request.Start),
                        End = NormalizePublicQueryTimestamp(timestamp: request.End)
                    }
                };
            case "All":
                return request with
                {
                    TemporalResult = new StorageTemporalAll()
                };
            default:
                throw new ArgumentException(message: "Unknown storage query construction request.", paramName: nameof(StorageQueryContext.Operation));
        }
    });

    private static DateTime NormalizePublicQueryTimestamp(DateTime timestamp) =>
        DateTime.SpecifyKind(value: timestamp, kind: DateTimeKind.Unspecified);
}