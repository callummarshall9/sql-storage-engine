// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal interface ICatalogAssemblyCatalogCodecProcessingService
{
    CatalogAssembly PrepareCatalogAssembly(CatalogAssembly catalogAssembly, byte[] image);

    byte[] ReadCatalogAssemblyImage(CatalogAssembly catalogAssembly);
}