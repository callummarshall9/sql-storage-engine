// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Managements.StorageGenerations;
internal sealed partial class StorageGenerationContextManagementService
{
    private void ValidateStorageGenerationContext(StorageGenerationContext storageGenerationContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(storageGenerationContext is null, nameof(storageGenerationContext)), (state.Database is null, nameof(StorageGenerationContext.Database))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ValidateStorageGenerationContextGenerated(StorageGenerationContext storageGenerationContext, CancellationToken cancellationToken)
    {
        ValidateStorageGenerationContext(storageGenerationContext: storageGenerationContext, cancellationToken: cancellationToken);
        Validate(inputs: [(storageGenerationContext.Kind == CatalogGeneratedAlwaysKind.None || !Enum.IsDefined(value: storageGenerationContext.Kind), nameof(StorageGenerationContext.Kind))]);
    }

    private void ValidateStorageGenerationContextPublication(StorageGenerationContext storageGenerationContext, CancellationToken cancellationToken)
    {
        ValidateStorageGenerationContext(storageGenerationContext: storageGenerationContext, cancellationToken: cancellationToken);
        Validate(inputs: [(storageGenerationContext.RootPageId.Value == 0, nameof(StorageGenerationContext.RootPageId))]);
    }

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "A valid database generation request is required.", paramName: input.ParameterName);
            }
        }
    }
}