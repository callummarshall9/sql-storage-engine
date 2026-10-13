// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Processings.SavepointFiles;
internal sealed partial class TransactionDirectorySavepointProcessingService
{
    private static void ValidateDatabasePath(string databasePath) =>
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: databasePath), databasePath is null, nameof(databasePath))]);

    private static void ValidateAllSavepointFilesOnDelete(string databasePath) =>
        Validate(inputs: [(string.IsNullOrWhiteSpace(value: databasePath), databasePath is null, nameof(databasePath))]);

    private static void Validate((bool Invalid, bool Missing, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (!input.Invalid)
            {
                continue;
            }

            if (input.Missing)
            {
                throw new ArgumentNullException(paramName: input.ParameterName);
            }

            throw new ArgumentException(message: "A database path is required.", paramName: input.ParameterName);
        }
    }
}