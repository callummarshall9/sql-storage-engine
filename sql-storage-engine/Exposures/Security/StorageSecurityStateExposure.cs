// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;
using sql_storage_engine.Services.Foundations.SecurityStates;
using sql_storage_engine.Brokers.SecuritySerialization;

namespace sql_storage_engine.Exposures.Security;
internal static class StorageSecurityStateExposure
{
    private static readonly ISecurityStateService service = new SecurityStateService(securitySerializationBroker: new SecuritySerializationBroker());
    internal static StorageSecurityState CreateStorageSecurityState() =>
        service.CreateSecurityStatePayload(securityStatePayload: new SecurityStatePayload())
            .State!;

    internal static byte[] Encode(StorageSecurityState state) =>
        service.EncodeSecurityStatePayload(securityStatePayload: new SecurityStatePayload { State = state });

    internal static StorageSecurityState Decode(byte[] bytes) =>
        service.DecodeSecurityStatePayload(securityStatePayload: new SecurityStatePayload { Bytes = bytes })
            .State!;
}