// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Foundations.SqlXmlSchemaCollections;

namespace sql_storage_engine.Exposures.Rows;
public static class SqlXmlSchemaCollectionExposure
{
    private static readonly ISqlXmlSchemaCollectionService service = new SqlXmlSchemaCollectionService();
    public static SqlXmlSchemaCollection CreateSqlXmlSchemaCollection(string schemaName, string name, string definition) =>
        service.CreateSqlXmlSchemaCollection(sqlXmlSchemaCollection: new SqlXmlSchemaCollection { SchemaName = schemaName, Name = name, DefinitionSource = [definition] });

    public static SqlXmlSchemaCollection CreateSqlXmlSchemaCollection(string schemaName, string name, IEnumerable<string> definitions) =>
        service.CreateSqlXmlSchemaCollection(sqlXmlSchemaCollection: new SqlXmlSchemaCollection { SchemaName = schemaName, Name = name, DefinitionSource = definitions });

    public static IReadOnlyList<string> ReadSqlXmlSchemaCollectionDefinitions(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        service.ReadSqlXmlSchemaCollectionDefinitions(sqlXmlSchemaCollection: sqlXmlSchemaCollection);

    public static string ReadSqlXmlSchemaCollectionDefinition(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        service.ReadSqlXmlSchemaCollectionDefinition(sqlXmlSchemaCollection: sqlXmlSchemaCollection);

    public static string ReadSqlXmlSchemaCollectionQualifiedName(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        service.ReadSqlXmlSchemaCollectionQualifiedName(sqlXmlSchemaCollection: sqlXmlSchemaCollection);

    internal static string CheckSqlXmlSchemaCollectionIdentifier(string value, string parameterName) =>
        service.CheckSqlXmlSchemaCollectionIdentifier(value: value, parameterName: parameterName);
}