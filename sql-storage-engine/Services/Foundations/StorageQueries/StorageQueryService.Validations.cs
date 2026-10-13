// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Foundations.StorageQueries;
internal sealed partial class StorageQueryService
{
    private static void ValidateStorageQueryContext(StorageQueryContext storageQueryContext)
    {
        Validate(inputs: [(storageQueryContext is null, nameof(storageQueryContext))]);
        var request = storageQueryContext!;
        Validate(inputs: [(request.Operation == "Bound" && request.Values is null, nameof(StorageQueryContext.Values)), (request.Operation is "Percent" or "Rows" && request.RepeatableSeed is < 0, nameof(StorageQueryContext.RepeatableSeed)), (request.Operation == "Percent" && request.Percentage is < 0 or > 100, nameof(StorageQueryContext.Percentage)), (request.Operation == "Rows" && request.RowCount < 0, nameof(StorageQueryContext.RowCount))]);
        Validate(inputs: [(request.Operation == "AsOf" && request.Instant.Kind == DateTimeKind.Local, nameof(StorageQueryContext.Instant)), (request.Operation is "FromTo" or "BetweenAnd" or "ContainedIn" && (request.Start.Kind == DateTimeKind.Local || request.End.Kind == DateTimeKind.Local || request.Start > request.End), nameof(StorageQueryContext.Start))]);
    }

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName, message: "Storage query options are invalid.");
            }
        }
    }
}