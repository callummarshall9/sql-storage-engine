// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Foundations.CatalogAssemblies;
internal sealed partial class CatalogAssemblyService
{
    private void ValidateCatalogAssemblySource(CatalogAssembly catalogAssembly, ReadOnlySpan<byte> image) =>
        ValidateCatalogAssemblyMetadata(catalogAssembly: catalogAssembly);

    private void ValidateCatalogAssemblyMetadata(CatalogAssembly catalogAssembly)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogAssembly);

        if (string.IsNullOrWhiteSpace(value: catalogAssembly.Name))
        {
            throw new ArgumentException(message: "SQL identifier cannot be empty.", paramName: "name");
        }

        if (catalogAssembly.Name.Length > 128)
        {
            throw new ArgumentOutOfRangeException(paramName: "name", message: "SQL identifiers cannot exceed 128 characters.");
        }

        if (!Enum.IsDefined(value: catalogAssembly.PermissionSet))
        {
            throw new ArgumentOutOfRangeException(paramName: "permissionSet");
        }

        if (catalogAssembly.Version is not null && !metadata.IsAssemblyVersion(version: catalogAssembly.Version))
        {
            throw new ArgumentException(message: "Assembly version is invalid.", paramName: "version");
        }

        if (catalogAssembly.PublicKeyToken is not null && (catalogAssembly.PublicKeyToken.Length != 16 || !catalogAssembly.PublicKeyToken.All(predicate: c => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f')))
        {
            throw new ArgumentException(message: "Public key token must contain 16 hexadecimal characters.", paramName: "publicKeyToken");
        }
    }

    private void ValidateCatalogAssemblyImage(CatalogAssembly catalogAssembly)
    {
        ValidateCatalogAssemblyMetadata(catalogAssembly: catalogAssembly);
        ArgumentNullException.ThrowIfNull(argument: catalogAssembly.ImageContent);

        if (catalogAssembly.ImageContent.Any(predicate: c => c > byte.MaxValue))
        {
            throw new ArgumentException(message: "Assembly image contains non-byte characters.", paramName: nameof(catalogAssembly));
        }
    }
}