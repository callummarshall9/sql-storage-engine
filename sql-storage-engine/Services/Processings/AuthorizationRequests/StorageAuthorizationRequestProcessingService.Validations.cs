// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Processings.AuthorizationRequests;
internal sealed partial class StorageAuthorizationRequestProcessingService
{
    private static void ValidateStorageAuthorizationRequest(StorageAuthorizationRequest storageAuthorizationRequest) =>
        Validate(inputs: [(storageAuthorizationRequest is null, nameof(storageAuthorizationRequest)), (storageAuthorizationRequest?.AccessSource is null && storageAuthorizationRequest?.Access is null, "access")]);

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }
        }
    }
}