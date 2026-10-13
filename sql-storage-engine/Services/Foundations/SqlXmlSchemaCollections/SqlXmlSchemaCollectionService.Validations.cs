// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlXmlSchemaCollections;
internal sealed partial class SqlXmlSchemaCollectionService
{
    private static void ValidateInput(SqlXmlSchemaCollection sqlXmlSchemaCollection)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlXmlSchemaCollection);
        _ = ValidateIdentifier(value: sqlXmlSchemaCollection.SchemaName, parameterName: "schemaName");
        _ = ValidateIdentifier(value: sqlXmlSchemaCollection.Name, parameterName: "name");
        ArgumentNullException.ThrowIfNull(argument: sqlXmlSchemaCollection.DefinitionSource);
    }

    private static void ValidateCollection(SqlXmlSchemaCollection sqlXmlSchemaCollection)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlXmlSchemaCollection);
        _ = ValidateIdentifier(value: sqlXmlSchemaCollection.SchemaName, parameterName: "schemaName");
        _ = ValidateIdentifier(value: sqlXmlSchemaCollection.Name, parameterName: "name");
        ArgumentNullException.ThrowIfNull(argument: sqlXmlSchemaCollection.DefinitionContent);
    }

    private static string ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value: value))
        {
            throw new ArgumentException("SQL identifier cannot be empty.", parameterName);
        }

        if (value.Length > 128)
        {
            throw new ArgumentOutOfRangeException(parameterName, "SQL identifiers cannot exceed 128 characters.");
        }

        return value;
    }

    private static void ValidateIdentifierArguments(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(argument: value);
        ArgumentNullException.ThrowIfNull(argument: parameterName);
    }
}