// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityStates;
internal interface ISecurityStateService
{
    SecurityStatePayload CreateSecurityStatePayload(SecurityStatePayload securityStatePayload);

    byte[] EncodeSecurityStatePayload(SecurityStatePayload securityStatePayload);

    SecurityStatePayload DecodeSecurityStatePayload(SecurityStatePayload securityStatePayload);
}