// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private void ValidateStorageSessionContextPublicTable(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ValidatePublicTableInputs(inputs: [(storageSessionContext is null, nameof(storageSessionContext)), (storageSessionContext?.PublicTable?.Handle?.Engine is null, nameof(StoragePublicTableContext.Handle)), (storageSessionContext?.PublicTable?.Storage?.Definition is null, nameof(StoragePublicTableContext.Storage)), (string.IsNullOrEmpty(value: storageSessionContext?.PublicTable?.Operation), nameof(StoragePublicTableContext.Operation))]);
        var request = storageSessionContext!.PublicTable!;
        ValidatePublicTableInputs(inputs: [(request.Operation is "Insert" or "TryInsert" && request.Row is null, nameof(StoragePublicTableContext.Row)), (request.Operation == "Update" && request.Update is null, nameof(StoragePublicTableContext.Update)), (request.Operation == "Sample" && request.Sample is null, nameof(StoragePublicTableContext.Sample)), (request.Operation == "Temporal" && request.TemporalQuery is null, nameof(StoragePublicTableContext.TemporalQuery))]);

        if (request.ReadOptions is not null)
        {
            ValidatePublicTableInputs(inputs: [(request.ReadOptions.UnmaskedColumns is null, nameof(StorageReadOptions.UnmaskedColumns))]);
        }

        if (request.Operation == "Sample")
        {
            var sample = request.Sample!;
            ValidatePublicTableInputs(inputs: [(sample.RepeatableSeed is < 0, nameof(StorageTableSample.RepeatableSeed)), (sample is StoragePercentTableSample percentage && percentage.Percentage is < 0 or > 100, nameof(StoragePercentTableSample.Percentage)), (sample is StorageRowsTableSample rows && rows.RowCount < 0, nameof(StorageRowsTableSample.RowCount)), (sample is not (StoragePercentTableSample or StorageRowsTableSample), nameof(StoragePublicTableContext.Sample))]);
        }

        if (request.Operation == "Temporal")
        {
            ValidatePublicTemporalQuery(storageTemporalQuery: request.TemporalQuery!);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidatePublicTemporalQuery(StorageTemporalQuery storageTemporalQuery)
    {
        var range = storageTemporalQuery switch
        {
            StorageTemporalAsOf asOf => (Start: asOf.Instant, End: asOf.Instant),
            StorageTemporalFromTo fromTo => (Start: fromTo.Start, End: fromTo.End),
            StorageTemporalBetweenAnd between => (Start: between.Start, End: between.End),
            StorageTemporalContainedIn contained => (Start: contained.Start, End: contained.End),
            StorageTemporalAll => (Start: DateTime.MinValue, End: DateTime.MaxValue),
            _ => throw new ArgumentException(message: "Unknown temporal query type.", paramName: nameof(storageTemporalQuery))};

        ValidatePublicTableInputs(inputs: [(range.Start.Kind == DateTimeKind.Local || range.End.Kind == DateTimeKind.Local || range.Start > range.End, nameof(storageTemporalQuery))]);
    }

    private static void ValidatePublicTableInputs((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "A valid public table request is required.", paramName: input.ParameterName);
            }
        }
    }
}