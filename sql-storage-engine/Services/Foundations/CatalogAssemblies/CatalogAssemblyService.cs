// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Brokers.Catalog;

namespace sql_storage_engine.Services.Foundations.CatalogAssemblies;
internal sealed partial class CatalogAssemblyService(AssemblyMetadataBroker metadata) : ICatalogAssemblyService
{
    public CatalogAssembly CreateCatalogAssembly(CatalogAssembly catalogAssembly, ReadOnlySpan<byte> image) =>
        TryCatch(operation: context =>
    {
        ValidateCatalogAssemblySource(catalogAssembly: context.catalogAssembly, image: context.image);

        return context.catalogAssembly with
        {
            PublicKeyToken = context.catalogAssembly.PublicKeyToken?.ToLowerInvariant(),
            ImageContent = string.Create(length: context.image.Length, state: context.image, action: static (destination, source) =>
            {
                for (var index = 0; index < source.Length; index++)
                {
                    destination[index] = (char)source[index];
                }
            })
        };
    }, context: new ReadableCatalogAssemblyContext { catalogAssembly = catalogAssembly, image = image });

    public ReadOnlyMemory<byte> ReadCatalogAssemblyImage(CatalogAssembly catalogAssembly) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogAssemblyImage(catalogAssembly: catalogAssembly);
        var bytes = new byte[catalogAssembly.ImageContent.Length];

        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)catalogAssembly.ImageContent[index];
        }

        return new ReadOnlyMemory<byte>(array: bytes);
    });

    public CatalogAssembly PrepareCatalogAssembly(CatalogAssembly catalogAssembly) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogAssemblyImage(catalogAssembly: catalogAssembly);

        return catalogAssembly with
        {
            PublicKeyToken = catalogAssembly.PublicKeyToken?.ToLowerInvariant()
        };
    });

    public void CheckCatalogAssembly(CatalogAssembly catalogAssembly) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogAssemblyImage(catalogAssembly: catalogAssembly);
    });

    internal static CatalogAssemblyService CreateCatalogAssemblyService() =>
        new CatalogAssemblyService(metadata: new sql_storage_engine.Brokers.Catalog.AssemblyMetadataBroker());
}