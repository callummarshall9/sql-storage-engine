// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Managements.CatalogStorage;
internal sealed partial class CatalogStorageManagementService : ICatalogStorageManagementService
{
    private readonly Dictionary<ulong, sql_storage_engine.Services.Coordinations.TableHeaps.TableHeapContextCoordinationService> heaps;
    private readonly Dictionary<ulong, sql_storage_engine.Services.Coordinations.IndexTrees.IndexTreeContextCoordinationService> trees;
    private readonly sql_storage_engine.Services.Coordinations.CatalogStorage.CatalogPublicationCoordinationService publication;
    private readonly CatalogStorageConfiguration configuration;
    private CatalogStorageManagementService(CatalogStorageConfiguration configuration, sql_storage_engine.Services.Coordinations.CatalogStorage.CatalogPublicationCoordinationService publication)
    {
        this.configuration = configuration;
        this.publication = publication;
        heaps = new();
        trees = new();
    }

    internal static CatalogStorageManagementService CreateCatalogStorageManagementService(CatalogStorageConfiguration configuration) =>
        new CatalogStorageManagementService(configuration: configuration, publication: sql_storage_engine.Services.Coordinations.CatalogStorage.CatalogPublicationCoordinationService.CreateCatalogPublicationCoordinationService(configuration: configuration));

    public ValueTask<CatalogStorageContext> ProcessCatalogStorageContextAsync(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCatalogStorageContext(catalogStorageContext: catalogStorageContext, cancellationToken: cancellationToken);

        switch (catalogStorageContext.Operation)
        {
            case 1:
            case 2:
                if (!heaps.TryGetValue(key: catalogStorageContext.Id, value: out var heap))
                {
                    heap = sql_storage_engine.Services.Coordinations.TableHeaps.TableHeapContextCoordinationService.CreateTableHeapContextCoordinationService(heapPageResourceConfiguration: configuration.Heap!with { RootPageId = catalogStorageContext.Operation == 1 ? 0 : catalogStorageContext.RootPageId.Value });

                    if (catalogStorageContext.Operation == 1)
                    {
                        await heap.CreateTableHeapContextAsync(tableHeapContext: new TableHeapContext(), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                    }
                    else
                    {
                        await heap.OpenTableHeapContextAsync(tableHeapContext: new TableHeapContext(), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                    }

                    heaps.Add(key: catalogStorageContext.Id, value: heap);
                }

                catalogStorageContext.RootPageId = heap.ReadTableHeapContext(tableHeapContext: new TableHeapContext())
            .RootPageId;
                catalogStorageContext.HeapBinding = new TableHeapBinding
                {
                    Service = heap,
                    RootPageId = catalogStorageContext.RootPageId
                };
                break;
            case 3:
                catalogStorageContext.Rows = ((sql_storage_engine.Services.Coordinations.TableHeaps.TableHeapContextCoordinationService)catalogStorageContext.HeapBinding!.Service!).ReadTableHeapContextScan(tableHeapContext: new TableHeapContext(), cancellationToken: cancellationToken)
            .Rows;
                break;
            case 4:
            case 6:
                var rootContext = catalogStorageContext.RootContext;
                if (!trees.TryGetValue(key: catalogStorageContext.Id, value: out var tree))
                {
                    rootContext ??= new IndexRootContext
                    {
                        RootPageId = catalogStorageContext.RootPageId.Value
                    };

                    tree = sql_storage_engine.Services.Coordinations.IndexTrees.IndexTreeContextCoordinationService.CreateIndexTreeContextCoordinationService(indexTreeResourceConfiguration: configuration.Tree!with { RootContext = rootContext, ReadRoot = null, UpdateRoot = null, RootSource = null, IsUnique = catalogStorageContext.IsUnique, Allocations = catalogStorageContext.Allocations });

                    if (catalogStorageContext.Operation == 4)
                    {
                        await tree.CreateIndexTreeContextAsync(indexTreeContext: new IndexTreeContext(), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                    }

                    trees.Add(key: catalogStorageContext.Id, value: tree);
                }

                catalogStorageContext.TreeBinding = new IndexTreeBinding
                {
                    Service = tree,
                    RootContext = rootContext
                };
                catalogStorageContext.RootPageId = tree.ReadIndexTreeContext(indexTreeContext: new IndexTreeContext())
            .RootPageId;
                break;
            case 5:
                var result = await ((sql_storage_engine.Services.Coordinations.IndexTrees.IndexTreeContextCoordinationService)catalogStorageContext.TreeBinding!.Service!).AddIndexTreeContextAsync(newIndexTreeContext: catalogStorageContext.Tree!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                catalogStorageContext.RootPageId = result.RootPageId;
                break;
            case 7:
                catalogStorageContext.RootPageId = ((sql_storage_engine.Services.Coordinations.IndexTrees.IndexTreeContextCoordinationService)catalogStorageContext.TreeBinding!.Service!).ReadIndexTreeContext(indexTreeContext: new IndexTreeContext())
            .RootPageId;
                break;
            case 14:
                heaps.Remove(key: catalogStorageContext.Id);
                break;
            case 15:
                trees.Remove(key: catalogStorageContext.Id);
                break;
            default:
                return await publication.ProcessCatalogStorageContextAsync(catalogStorageContext: catalogStorageContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return catalogStorageContext;
    });

    public CatalogStorageContext ResetCatalogStorageContext(CatalogStorageContext catalogStorageContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogStorageResetContext(catalogStorageContext: catalogStorageContext);
        heaps.Clear();
        trees.Clear();
        return catalogStorageContext;
    });
}