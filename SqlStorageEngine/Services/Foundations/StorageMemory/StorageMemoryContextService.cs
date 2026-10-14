// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Storage;
using System.Linq;
using sql_storage_engine.Brokers.StorageMemoryResources;

namespace sql_storage_engine.Services.Foundations.StorageMemory;
internal sealed partial class StorageMemoryContextService : IStorageMemoryContextService
{
    private readonly StorageMemoryResourceBroker resource = new StorageMemoryResourceBroker();

    public ValueTask<StorageStrategyContext> ProcessStorageStrategyContextAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageStrategyContext(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken);

        switch (storageStrategyContext.Operation)
        {
            case 100:
                storageStrategyContext.State!.Runtime = resource.Create();
                break;
            case 101:
                await resource.EnterAsync(handle: storageStrategyContext.State!.Runtime, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 102:
                resource.Exit(handle: storageStrategyContext.State!.Runtime);
                break;
            case 103:
                resource.Close(handle: storageStrategyContext.State!.Runtime);
                break;
            case 110:
                if (storageStrategyContext.State!.Cache!.TryGetValue(key: storageStrategyContext.SegmentIndex, value: out var bytes))
                {
                    storageStrategyContext.Bytes = bytes;

                    storageStrategyContext.State.Accesses![storageStrategyContext.SegmentIndex] = checked(++storageStrategyContext.State.Clock);
                }
                else
                {
                    storageStrategyContext.Bytes = null;
                }

                break;
            case 111:
                CacheSegment(storageStrategyContext: storageStrategyContext);
                break;
            case 112:
                storageStrategyContext.State!.Cache!.Clear();

                storageStrategyContext.State.Accesses!.Clear();

                storageStrategyContext.State.Dirty!.Clear();

                storageStrategyContext.State.DirtyPages!.Clear();

                storageStrategyContext.State.CacheBytes = 0;
                break;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(storageStrategyContext.Operation));
        }

        return storageStrategyContext;
    });

    private static void CacheSegment(StorageStrategyContext storageStrategyContext)
    {
        var state = storageStrategyContext.State!;

        var bytes = storageStrategyContext.Bytes!;

        var previousLength = state.Cache!.TryGetValue(key: storageStrategyContext.SegmentIndex, value: out var previous) ? previous.Length : 0;

        var needed = checked(state.CacheBytes - previousLength + bytes.Length);

        while (needed > state.Options!.MaximumCacheBytes)
        {
            var candidates = state.Accesses!.Where(predicate: item => item.Key != storageStrategyContext.SegmentIndex && !state.Dirty!.Contains(item: item.Key))
                .OrderBy(keySelector: item => item.Value)
                .ToArray();

            if (candidates.Length == 0)
            {
                throw new StorageResourceExhaustedException(message: "The memory cache is full of dirty segments; commit before adding more pages.");
            }

            var victim = candidates[0].Key;

            var removed = state.Cache[victim].Length;

            state.Cache.Remove(key: victim);

            state.Accesses!.Remove(key: victim);

            state.CacheBytes -= removed;

            needed -= removed;
        }

        state.Cache[storageStrategyContext.SegmentIndex] = bytes;

        state.Accesses![storageStrategyContext.SegmentIndex] = checked(++state.Clock);

        state.CacheBytes = needed;
    }
}