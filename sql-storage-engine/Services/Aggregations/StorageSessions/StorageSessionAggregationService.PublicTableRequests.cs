// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Tables;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    public ValueTask<StorageSessionContext> ExecuteStorageSessionContextPublicTableAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextPublicTable(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var request = storageSessionContext.PublicTable!;

        switch (request.Operation)
        {
            case "Insert":
                var id = await PublicTableInsertAsync(storagePublicTableContext: request, row: request.Row!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                request.InsertResult = new TableInsertResult
                {
                    Inserted = true,
                    RowId = id,
                    Warnings = []
                };
                break;
            case "TryInsert":
                request.InsertResult = await PublicTableTryInsertAsync(storagePublicTableContext: request, row: request.Row!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "Get":
                request.StoredRow = request.ReadOptions is null ? await PublicTableGetAsync(storagePublicTableContext: request, rowId: request.RowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) : await PublicTableGetAsync(storagePublicTableContext: request, rowId: request.RowId, readOptions: request.ReadOptions, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "Update":
                request.UpdateResult = await PublicTableUpdateAsync(storagePublicTableContext: request, rowId: request.RowId, update: request.Update!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "Delete":
                request.DeleteResult = await PublicTableDeleteAsync(storagePublicTableContext: request, rowId: request.RowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            default:
                throw new ArgumentException(message: "Unknown table mutation request.", paramName: nameof(StoragePublicTableContext.Operation));
        }

        return storageSessionContext;
    });

    public StorageSessionContext ReadStorageSessionContextPublicTableScan(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateStorageSessionContextPublicTable(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var request = storageSessionContext.PublicTable!;

        if (request.Operation == "Temporal")
        {
            var rows = request.ReadOptions is null ? PublicTableTemporalScanAsyncCore(storagePublicTableContext: request, query: request.TemporalQuery!, cancellationToken: cancellationToken) : PublicTableTemporalScanAsyncCore(storagePublicTableContext: request, query: request.TemporalQuery!, readOptions: request.ReadOptions, cancellationToken: cancellationToken);
            request.TemporalRows = TrackPublicTableStream(storagePublicTableContext: request, source: rows);
        }
        else
        {
            var rows = request.Operation switch
            {
                "Scan" => request.ReadOptions is null ? PublicTableScanAsyncCore(storagePublicTableContext: request, cancellationToken: cancellationToken) : PublicTableScanAsyncCore(storagePublicTableContext: request, readOptions: request.ReadOptions, cancellationToken: cancellationToken),
                "Sample" => request.ReadOptions is null ? PublicTableSampleAsyncCore(storagePublicTableContext: request, sample: request.Sample!, cancellationToken: cancellationToken) : PublicTableSampleAsyncCore(storagePublicTableContext: request, sample: request.Sample!, readOptions: request.ReadOptions, cancellationToken: cancellationToken),
                _ => throw new ArgumentException(message: "Unknown table scan request.", paramName: nameof(StoragePublicTableContext.Operation))};

            request.Rows = TrackPublicTableStream(storagePublicTableContext: request, source: rows);
        }

        return storageSessionContext;
    });

    private IAsyncEnumerable<T> TrackPublicTableStream<T>(StoragePublicTableContext storagePublicTableContext, IAsyncEnumerable<T> source)
    {
        var resources = (EngineResourceContext)storagePublicTableContext.Handle!.Engine!.ResourceSource!;

        var access = storagePublicTableContext.Coordinate ? null : engineResources.ReadEngineResourceContextAccess(engineResourceContext: resources)
            .Access;

        return (IAsyncEnumerable<T>)engineResources.TrackEngineResourceContextStream<T>(engineResourceContext: resources with { StreamSource = source, Access = access })
            .StreamSource!;
    }
}