// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Processings.AuthorizationRequests;
internal interface IStorageAuthorizationRequestProcessingService
{
    StorageAuthorizationRequest PrepareStorageAuthorizationRequest(StorageAuthorizationRequest storageAuthorizationRequest);
}