// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Foundations.SqlTypeDocuments;
internal sealed partial class SqlTypeDocumentService
{
    private static void ValidateSchemas(string[] definitions)
    {
        ArgumentNullException.ThrowIfNull(argument: definitions);
    }

    private static void ValidateXml(string value, bool isDocument, string[]? definitions, string constraint, string columnName)
    {
        ArgumentNullException.ThrowIfNull(argument: value);
        ArgumentNullException.ThrowIfNull(argument: constraint);
        ArgumentNullException.ThrowIfNull(argument: columnName);
    }

    private static void ValidateJson(string value, string columnName)
    {
        ArgumentNullException.ThrowIfNull(argument: value);
        ArgumentNullException.ThrowIfNull(argument: columnName);
    }

    private static void ValidateReadDocument(string value)
    {
        ArgumentNullException.ThrowIfNull(argument: value);
    }

    private static void ValidateWriteJsonVector(float[] values)
    {
        ArgumentNullException.ThrowIfNull(argument: values);
    }
}