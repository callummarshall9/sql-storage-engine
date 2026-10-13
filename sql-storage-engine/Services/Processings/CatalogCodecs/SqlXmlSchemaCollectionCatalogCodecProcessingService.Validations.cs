// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal sealed partial class SqlXmlSchemaCollectionCatalogCodecProcessingService
{
    private static void ValidateSqlXmlSchemaCollection(SqlXmlSchemaCollection sqlXmlSchemaCollection)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlXmlSchemaCollection);
    }
}