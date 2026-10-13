// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Coordinations.CatalogCodecs;
internal interface ICatalogTableSchemasCoordinationService
{
    CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext);
}