// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Processings.AuthorizationRequests;
internal sealed partial class StorageAuthorizationRequestProcessingService : IStorageAuthorizationRequestProcessingService
{
    public StorageAuthorizationRequest PrepareStorageAuthorizationRequest(StorageAuthorizationRequest storageAuthorizationRequest) =>
        TryCatch(operation: () =>
    {
        ValidateStorageAuthorizationRequest(storageAuthorizationRequest: storageAuthorizationRequest);

        var access = (storageAuthorizationRequest.AccessSource ?? storageAuthorizationRequest.Access).Take(count: 257)
            .ToArray();

        return new StorageAuthorizationRequest
        {
            Principal = storageAuthorizationRequest.Principal,
            Revision = storageAuthorizationRequest.Revision,
            OperationId = storageAuthorizationRequest.OperationId,
            CorrelationId = storageAuthorizationRequest.CorrelationId,
            Access = Array.AsReadOnly(array: access)
        };
    });
}