// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal sealed partial class SecurityStateCatalogCodecProcessingService
{
    private static void ValidateSecurityStatePayload(SecurityStatePayload securityStatePayload)
    {
        ArgumentNullException.ThrowIfNull(argument: securityStatePayload);
    }
}