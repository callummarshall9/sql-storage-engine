// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal sealed partial class SecurityStateCatalogCodecProcessingService : ISecurityStateCatalogCodecProcessingService
{
    private readonly sql_storage_engine.Services.Foundations.SecurityStates.SecurityStateService states;
    public SecurityStateCatalogCodecProcessingService(sql_storage_engine.Services.Foundations.SecurityStates.SecurityStateService states)
    {
        this.states = states;
    }

    public byte[] EncodeSecurityStatePayload(SecurityStatePayload securityStatePayload) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityStatePayload(securityStatePayload: securityStatePayload);
        return states.EncodeSecurityStatePayload(securityStatePayload: securityStatePayload);
    });

    public SecurityStatePayload DecodeSecurityStatePayload(SecurityStatePayload securityStatePayload) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityStatePayload(securityStatePayload: securityStatePayload);
        return states.DecodeSecurityStatePayload(securityStatePayload: securityStatePayload);
    });

    internal static SecurityStateCatalogCodecProcessingService CreateSecurityStateCatalogCodecProcessingService() =>
        new SecurityStateCatalogCodecProcessingService(states: sql_storage_engine.Services.Foundations.SecurityStates.SecurityStateService.CreateSecurityStateService());
}