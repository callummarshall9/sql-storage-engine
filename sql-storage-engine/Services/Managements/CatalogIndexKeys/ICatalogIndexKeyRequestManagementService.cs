// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Managements.CatalogIndexKeys;
internal interface ICatalogIndexKeyRequestManagementService
{
    CatalogIndexKeyRequest PrepareCatalogIndexKeyRequest(CatalogIndexKeyRequest catalogIndexKeyRequest);
}