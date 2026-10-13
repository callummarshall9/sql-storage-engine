// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogCheckConstraintProcessingService
{
    private static void ValidateCatalogCheckConstraint(CatalogCheckConstraint catalogCheckConstraint)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogCheckConstraint);
        var name = catalogCheckConstraint.Name;
        var expression = catalogCheckConstraint.Expression;
        ValidateIdentifier(value: name, parameterName: nameof(name));

        if (string.IsNullOrWhiteSpace(value: expression))
        {
            throw new ArgumentException(message: "Check expression cannot be empty.", paramName: nameof(expression));
        }
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value: value))
        {
            throw new ArgumentException(message: "SQL identifier cannot be empty.", paramName: parameterName);
        }

        if (value.Length > 128)
        {
            throw new ArgumentOutOfRangeException(paramName: parameterName, message: "SQL identifiers cannot exceed 128 characters.");
        }
    }
}