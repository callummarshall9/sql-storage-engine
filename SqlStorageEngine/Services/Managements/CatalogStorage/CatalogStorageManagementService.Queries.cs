// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Services.Coordinations.IndexTrees;

namespace sql_storage_engine.Services.Managements.CatalogStorage;
internal sealed partial class CatalogStorageManagementService
{
    public ValueTask<CatalogStorageContext> ReadCatalogStorageContextIndexEntriesAsync(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCatalogStorageIndexQuery(catalogStorageContext: catalogStorageContext, cancellationToken: cancellationToken, range: false);
        var service = (IndexTreeContextCoordinationService)catalogStorageContext.TreeBinding!.Service!;

        var result = await service.ReadIndexTreeContextEntriesAsync(indexTreeContext: catalogStorageContext.Tree!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        catalogStorageContext.Entries = result.Entries;
        return catalogStorageContext;
    });

    public CatalogStorageContext ReadCatalogStorageContextIndexScan(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogStorageIndexQuery(catalogStorageContext: catalogStorageContext, cancellationToken: cancellationToken, range: true);
        var service = (IndexTreeContextCoordinationService)catalogStorageContext.TreeBinding!.Service!;

        catalogStorageContext.EntryStream = service.ReadIndexTreeContextScan(indexTreeContext: catalogStorageContext.Tree!, cancellationToken: cancellationToken)
            .Rows;

        return catalogStorageContext;
    });

    public ValueTask<CatalogStorageContext> ReadCatalogStorageContextHeapPagesAsync(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCatalogStoragePageQuery(catalogStorageContext: catalogStorageContext, cancellationToken: cancellationToken, index: false);
        var service = (sql_storage_engine.Services.Coordinations.TableHeaps.TableHeapContextCoordinationService)catalogStorageContext.HeapBinding!.Service!;

        catalogStorageContext.PageIds = (await service.ReadTableHeapContextPageIdsAsync(tableHeapContext: new sql_storage_engine.Models.Heap.TableHeapContext { MaximumPages = int.MaxValue }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PageIds;

        return catalogStorageContext;
    });

    public ValueTask<CatalogStorageContext> ReadCatalogStorageContextIndexPagesAsync(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCatalogStoragePageQuery(catalogStorageContext: catalogStorageContext, cancellationToken: cancellationToken, index: true);
        var service = (IndexTreeContextCoordinationService)catalogStorageContext.TreeBinding!.Service!;

        catalogStorageContext.PageIds = (await service.ReadIndexTreeContextLeafPagesAsync(indexTreeContext: new IndexTreeContext(), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PageIds;

        return catalogStorageContext;
    });

    private static void ValidateCatalogStoragePageQuery(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken, bool index)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogStorageContext);

        if (index)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogStorageContext.TreeBinding?.Service);
        }
        else
        {
            ArgumentNullException.ThrowIfNull(argument: catalogStorageContext.HeapBinding?.Service);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateCatalogStorageIndexQuery(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken, bool range)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogStorageContext);
        ArgumentNullException.ThrowIfNull(argument: catalogStorageContext.TreeBinding);
        ArgumentNullException.ThrowIfNull(argument: catalogStorageContext.TreeBinding.Service);
        ArgumentNullException.ThrowIfNull(argument: catalogStorageContext.Tree);

        if (range)
        {
            ArgumentNullException.ThrowIfNull(argument: catalogStorageContext.Tree.Range);
        }
        else
        {
            ArgumentNullException.ThrowIfNull(argument: catalogStorageContext.Tree.Key);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}