// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlXmlSchemaCollections;
internal interface ISqlXmlSchemaCollectionService
{
    SqlXmlSchemaCollection CreateSqlXmlSchemaCollection(SqlXmlSchemaCollection sqlXmlSchemaCollection);

    IReadOnlyList<string> ReadSqlXmlSchemaCollectionDefinitions(SqlXmlSchemaCollection sqlXmlSchemaCollection);

    string ReadSqlXmlSchemaCollectionDefinition(SqlXmlSchemaCollection sqlXmlSchemaCollection);

    string ReadSqlXmlSchemaCollectionQualifiedName(SqlXmlSchemaCollection sqlXmlSchemaCollection);

    string CheckSqlXmlSchemaCollectionIdentifier(string value, string parameterName);
}