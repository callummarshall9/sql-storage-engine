// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Services.Foundations.DatabaseIds;
internal sealed partial class DatabaseIdService
{
    private static void ValidateDatabaseIdOnCreate(DatabaseId databaseId) =>
        Validate(inputs: [(databaseId.Value == Guid.Empty, nameof(databaseId))]);

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "A new database identity must not be empty.", paramName: input.ParameterName);
            }
        }
    }
}