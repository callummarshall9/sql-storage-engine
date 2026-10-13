// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal interface ISecurityStateCatalogCodecProcessingService
{
    byte[] EncodeSecurityStatePayload(SecurityStatePayload securityStatePayload);

    SecurityStatePayload DecodeSecurityStatePayload(SecurityStatePayload securityStatePayload);
}