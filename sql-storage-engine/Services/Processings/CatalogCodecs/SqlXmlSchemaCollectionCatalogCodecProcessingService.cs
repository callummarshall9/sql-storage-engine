// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal sealed partial class SqlXmlSchemaCollectionCatalogCodecProcessingService : ISqlXmlSchemaCollectionCatalogCodecProcessingService
{
    private readonly sql_storage_engine.Services.Foundations.SqlXmlSchemaCollections.SqlXmlSchemaCollectionService schemas;
    public SqlXmlSchemaCollectionCatalogCodecProcessingService(sql_storage_engine.Services.Foundations.SqlXmlSchemaCollections.SqlXmlSchemaCollectionService schemas)
    {
        this.schemas = schemas;
    }

    public SqlXmlSchemaCollection PrepareSqlXmlSchemaCollection(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        TryCatch(operation: () =>
    {
        ValidateSqlXmlSchemaCollection(sqlXmlSchemaCollection: sqlXmlSchemaCollection);
        return schemas.CreateSqlXmlSchemaCollection(sqlXmlSchemaCollection: sqlXmlSchemaCollection);
    });

    public IReadOnlyList<string> ReadSqlXmlSchemaCollectionDefinitions(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        TryCatch(operation: () =>
    {
        ValidateSqlXmlSchemaCollection(sqlXmlSchemaCollection: sqlXmlSchemaCollection);
        return schemas.ReadSqlXmlSchemaCollectionDefinitions(sqlXmlSchemaCollection: sqlXmlSchemaCollection);
    });

    internal static SqlXmlSchemaCollectionCatalogCodecProcessingService CreateSqlXmlSchemaCollectionCatalogCodecProcessingService() =>
        new SqlXmlSchemaCollectionCatalogCodecProcessingService(schemas: new sql_storage_engine.Services.Foundations.SqlXmlSchemaCollections.SqlXmlSchemaCollectionService());
}