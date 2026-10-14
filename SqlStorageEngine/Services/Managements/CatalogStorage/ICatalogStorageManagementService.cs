// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Managements.CatalogStorage;
internal interface ICatalogStorageManagementService
{
    ValueTask<CatalogStorageContext> ProcessCatalogStorageContextAsync(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken);

    ValueTask<CatalogStorageContext> ReadCatalogStorageContextIndexEntriesAsync(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken);

    CatalogStorageContext ReadCatalogStorageContextIndexScan(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken);

    ValueTask<CatalogStorageContext> ReadCatalogStorageContextHeapPagesAsync(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken);

    ValueTask<CatalogStorageContext> ReadCatalogStorageContextIndexPagesAsync(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken);

    CatalogStorageContext ResetCatalogStorageContext(CatalogStorageContext catalogStorageContext);
}