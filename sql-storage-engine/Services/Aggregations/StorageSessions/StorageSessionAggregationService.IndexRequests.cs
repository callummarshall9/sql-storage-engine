// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    public ValueTask<StorageSessionContext> ReadStorageSessionContextIndexAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionIndexRequest(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var context = storageSessionContext.Index!;

        switch (context.Operation)
        {
            case "Find":
                context.RowIds = await IndexFindAsync(storageIndexContext: context, values: context.Values!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "Entries":
                context.Entries = await IndexFindEntriesAsync(storageIndexContext: context, values: context.Values!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "Nearest":
                context.Matches = await IndexSearchNearestAsync(storageIndexContext: context, query: context.Query!, count: context.Count, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "JsonValue":
                context.RowIds = await IndexFindJsonPathAsync(storageIndexContext: context, path: context.Path!, value: context.Query!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "JsonExists":
                context.RowIds = await IndexFindJsonPathExistsAsync(storageIndexContext: context, path: context.Path!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "Xml":
                context.RowIds = await IndexFindXmlPathAsync(storageIndexContext: context, path: context.Path!, value: context.Text, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            default:
                throw new ArgumentException(message: "Unsupported public index query.", paramName: nameof(storageSessionContext));
        }

        return storageSessionContext;
    });

    public StorageSessionContext ReadStorageSessionContextIndexScan(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateStorageSessionIndexRequest(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var context = storageSessionContext.Index!;

        switch (context.Operation)
        {
            case "Range":
                context.RowStream = TrackPublicIndexStream(storageIndexContext: context, source: IndexScanAsyncCore(storageIndexContext: context, range: context.Range!, cancellationToken: cancellationToken));
                break;
            case "Values":
                context.RowStream = TrackPublicIndexStream(storageIndexContext: context, source: IndexScanAsyncCore(storageIndexContext: context, lowerBound: context.LowerValues!, upperBound: context.UpperValues!, includeLowerBound: context.IncludeLower, includeUpperBound: context.IncludeUpper, direction: context.Direction, cancellationToken: cancellationToken));
                break;
            case "Entries":
                context.EntryStream = TrackPublicIndexStream(storageIndexContext: context, source: IndexScanEntriesAsyncCore(storageIndexContext: context, range: context.Range!, cancellationToken: cancellationToken));
                break;
            case "FullText":
                context.FullTextStream = TrackPublicIndexStream(storageIndexContext: context, source: IndexSearchFullTextAsyncCore(storageIndexContext: context, request: context.FullText!, cancellationToken: cancellationToken));
                break;
            default:
                throw new ArgumentException(message: "Unsupported public index stream.", paramName: nameof(storageSessionContext));
        }

        return storageSessionContext;
    });

    private static void ValidateStorageSessionIndexRequest(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ValidatePublicIndexInputs(inputs: [(storageSessionContext is null, nameof(storageSessionContext)), (storageSessionContext?.Index is null, nameof(StorageSessionContext.Index)), (storageSessionContext?.Index?.Handle?.Engine is null, nameof(StorageIndexContext.Handle)), (storageSessionContext?.Index?.Index?.Definition is null, nameof(StorageIndexContext.Index)), (storageSessionContext?.Index?.Table is null, nameof(StorageIndexContext.Table)), (storageSessionContext?.Index?.Operation is null, nameof(StorageIndexContext.Operation))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidatePublicIndexInputs((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "A bound public index query is required.", paramName: input.ParameterName);
            }
        }
    }
}