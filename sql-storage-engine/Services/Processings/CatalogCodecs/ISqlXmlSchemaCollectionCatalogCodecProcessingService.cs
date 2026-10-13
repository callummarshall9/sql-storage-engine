// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal interface ISqlXmlSchemaCollectionCatalogCodecProcessingService
{
    SqlXmlSchemaCollection PrepareSqlXmlSchemaCollection(SqlXmlSchemaCollection sqlXmlSchemaCollection);

    IReadOnlyList<string> ReadSqlXmlSchemaCollectionDefinitions(SqlXmlSchemaCollection sqlXmlSchemaCollection);
}