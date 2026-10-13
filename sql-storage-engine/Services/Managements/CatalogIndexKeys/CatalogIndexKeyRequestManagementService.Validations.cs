// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Managements.CatalogIndexKeys;
internal sealed partial class CatalogIndexKeyRequestManagementService
{
    private static void ValidateCatalogIndexKeyRequest(CatalogIndexKeyRequest catalogIndexKeyRequest)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest);

        switch (catalogIndexKeyRequest.Operation)
        {
            case 15:
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Type);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.DefaultCollation);
                break;
            case 1:
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Row);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Table);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Index);
                break;
            case 12:
            case 2:
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Index);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Path);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.SqlValue);
                break;
            case 13:
            case 3:
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Index);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Path);
                break;
            case 4:
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Index);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Path);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Value);
                break;
            case 5:
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Index);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Path);
                break;
            case 6:
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Index);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Table);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Request);
                break;
            case 7:
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Row);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Table);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Index);
                break;
            case 8:
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Row);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Table);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Index);
                break;
            case 9:
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Payload);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Table);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Index);
                break;
            case 14:
            case 10:
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Values);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Table);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Index);
                break;
            case 11:
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Values);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Table);
                ArgumentNullException.ThrowIfNull(argument: catalogIndexKeyRequest.Index);
                break;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(catalogIndexKeyRequest));
        }
    }

    private static void ValidateCatalogIndexProjectionContext(CatalogIndexProjectionContext catalogIndexProjectionContext) =>
        ArgumentNullException.ThrowIfNull(argument: catalogIndexProjectionContext);
}