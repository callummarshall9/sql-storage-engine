// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Foundations.CatalogAssemblies;
internal interface ICatalogAssemblyService
{
    CatalogAssembly CreateCatalogAssembly(CatalogAssembly catalogAssembly, ReadOnlySpan<byte> image);

    ReadOnlyMemory<byte> ReadCatalogAssemblyImage(CatalogAssembly catalogAssembly);

    CatalogAssembly PrepareCatalogAssembly(CatalogAssembly catalogAssembly);

    void CheckCatalogAssembly(CatalogAssembly catalogAssembly);
}