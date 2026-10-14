// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Coordinations.CatalogStorage;
internal interface ICatalogPublicationCoordinationService
{
    ValueTask<CatalogStorageContext> ProcessCatalogStorageContextAsync(CatalogStorageContext catalogStorageContext, CancellationToken cancellationToken);
}