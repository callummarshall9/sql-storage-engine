// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    public ValueTask<StorageSessionContext> CreateStorageSessionContextEngineAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextEngineCreation(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var request = storageSessionContext.Creation!;

        var options = request.Options ?? new StorageEngineOptions
        {
            PageSize = sql_storage_engine.Models.Pages.PageConstants.DefaultSize,
            BufferPoolCapacity = 256,
            InlineValueThreshold = 1024,
            DefaultCollation = "SQL_Latin1_General_CP1_CI_AS",
            TimeProvider = TimeProvider.System
        };

        var input = new EngineResourceContext
        {
            Path = request.Path,
            PageSize = (int)options.PageSize,
            BufferCapacity = options.BufferPoolCapacity,
            DefaultCollation = options.DefaultCollation,
            InlineValueThreshold = options.InlineValueThreshold
        };

        var resources = request.Creating ? await engineResources.CreateEngineResourceContextAsync(engineResourceContext: input, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) : await engineResources.OpenEngineResourceContextAsync(engineResourceContext: input, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var engine = new StorageEngineRuntime
            {
                ResourceSource = resources
            };

            var bound = CreateStorageSessionAggregationService(configuration: new StorageSessionConfiguration { EngineResourcesBinding = new EngineResourceBinding { Service = engineResources, Context = resources }, CatalogStorage = resources.CatalogStorage, Rows = resources.Rows, Overflow = resources.OverflowConfiguration, Generation = new StorageGenerationConfiguration { DatabaseBinding = resources.DatabaseBinding, TimeProviderSource = options.TimeProvider } });

            if (resources.Header!.CatalogRootPageId is { } root)
            {
                await bound.ExecuteCatalogRequestAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 100, RootPageId = root } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            storageSessionContext.Binding = new StorageSessionBinding
            {
                Service = bound,
                Engine = engine
            };

            return storageSessionContext;
        }
        catch
        {
            await engineResources.CloseEngineResourceContextAsync(engineResourceContext: resources, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

            throw;
        }
    });

    public ValueTask<StorageSessionContext> ReadStorageSessionContextEngineHandleAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextEngineHandle(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var request = storageSessionContext.OpenHandle!;

        var handle = request.Handle!with
        {
            Generation = request.Handle!.Engine!.Generation
        };

        EnsureStorageIndexCurrent(storageIndexContext: new StorageIndexContext { Handle = handle });
        var resource = (EngineResourceContext)handle.Engine!.ResourceSource!;

        var lease = await engineResources.EnterEngineResourceContextAsync(engineResourceContext: resource with { Reentrant = true }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lifetime = lease.Lifetime!;

        using var active = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        handle = handle with
        {
            Generation = handle.Engine.Generation
        };

        EnsureStorageIndexCurrent(storageIndexContext: new StorageIndexContext { Handle = handle });

        var table = await ReadCatalogTableStorageContextAsync(tableId: request.IndexId is { } requestedIndex ? ReadCatalogIndexBinding(indexId: requestedIndex)
            .Definition!.TableId : request.TableId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var tableHandle = new StoragePublicTableContext
        {
            Handle = handle,
            Storage = table,
            Coordinate = handle.Statement is null,
            FlushMutations = handle.Statement is null
        };

        if (request.IndexId is { } indexId)
        {
            storageSessionContext.OpenHandle!.Result = new StorageSessionContext
            {
                Binding = new StorageSessionBinding
                {
                    Service = this,
                    Engine = handle.Engine
                },
                Engine = handle.Engine,
                Statement = handle.Statement,
                Index = new StorageIndexContext
                {
                    Handle = handle,
                    Index = ReadCatalogIndexBinding(indexId: indexId),
                    Table = table.Definition
                }
            };
        }
        else if (request.GraphKind != 0)
        {
            var expectedKind = request.GraphKind == 1 ? sql_storage_engine.Catalog.GraphTableKind.Node : sql_storage_engine.Catalog.GraphTableKind.Edge;

            if (request.GraphKind > 2 || table.Definition!.Graph?.Kind != expectedKind)
            {
                throw new InvalidOperationException(message: "The requested table is not a registered graph table of the requested kind.");
            }

            storageSessionContext.OpenHandle!.Result = new StorageSessionContext
            {
                Binding = new StorageSessionBinding
                {
                    Service = this,
                    Engine = handle.Engine
                },
                Engine = handle.Engine,
                Statement = handle.Statement,
                Graph = new StorageGraphContext
                {
                    Handle = handle,
                    Storage = table
                }
            };
        }
        else
        {
            storageSessionContext.OpenHandle!.Result = new StorageSessionContext
            {
                Binding = new StorageSessionBinding
                {
                    Service = this,
                    Engine = handle.Engine
                },
                Engine = handle.Engine,
                Statement = handle.Statement,
                PublicTable = tableHandle
            };
        }

        return storageSessionContext;
    });

    private static void ValidateStorageSessionContextEngineCreation(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ValidatePublicTableInputs(inputs: [(storageSessionContext is null, nameof(storageSessionContext)), (string.IsNullOrWhiteSpace(value: storageSessionContext?.Creation?.Path), nameof(StorageEngineCreationContext.Path))]);
        var options = storageSessionContext!.Creation!.Options;
        ValidatePublicTableInputs(inputs: [(options?.BufferPoolCapacity <= 0, nameof(StorageEngineOptions.BufferPoolCapacity)), (options?.InlineValueThreshold < 0, nameof(StorageEngineOptions.InlineValueThreshold)), (options is not null && options.TimeProvider is null, nameof(StorageEngineOptions.TimeProvider))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateStorageSessionContextEngineHandle(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ValidatePublicTableInputs(inputs: [(storageSessionContext is null, nameof(storageSessionContext)), (storageSessionContext?.OpenHandle?.Handle?.Engine is null, nameof(StorageEngineOpenHandleContext.Handle))]);
        cancellationToken.ThrowIfCancellationRequested();
    }
}