// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Services.Processings.AuthorizationRequests;

namespace sql_storage_engine.Exposures.Security;
/// <summary>Prepares bounded immutable authorization-request snapshots.</summary>
public static class StorageAuthorizationRequestExposure
{
    private static readonly IStorageAuthorizationRequestProcessingService service = new StorageAuthorizationRequestProcessingService();
    public static StorageAuthorizationRequest CreateStorageAuthorizationRequest(StoragePrincipalId storagePrincipalId, long revision, Guid operationId, Guid correlationId, IEnumerable<StorageRequiredAccess> access) =>
        service.PrepareStorageAuthorizationRequest(storageAuthorizationRequest: new StorageAuthorizationRequest { Principal = storagePrincipalId, Revision = revision, OperationId = operationId, CorrelationId = correlationId, AccessSource = access });

    public static StorageAuthorizationRequest PrepareStorageAuthorizationRequest(StorageAuthorizationRequest storageAuthorizationRequest) =>
        service.PrepareStorageAuthorizationRequest(storageAuthorizationRequest: storageAuthorizationRequest);
}