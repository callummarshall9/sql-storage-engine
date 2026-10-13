// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers;
using System.Linq;
using System;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.CatalogCodecs;
internal sealed partial class CatalogDefinitionTransportCoordinationService : ICatalogDefinitionTransportCoordinationService
{
    private readonly sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogDefinitionTypesOrchestrationService catalogDefinitionTypes;
    private readonly sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogWireValuesOrchestrationService catalogWireValues;
    public CatalogDefinitionTransportCoordinationService(sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogDefinitionTypesOrchestrationService catalogDefinitionTypes, sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogWireValuesOrchestrationService catalogWireValues)
    {
        this.catalogDefinitionTypes = catalogDefinitionTypes;
        this.catalogWireValues = catalogWireValues;
    }

    public CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogCodecContext(catalogCodecContext: catalogCodecContext);

        switch (catalogCodecContext.Operation)
        {
            case 18:
                var definition = catalogCodecContext.CatalogDefinition!;
                ArgumentNullException.ThrowIfNull(argument: definition);
                definition = definition with
                {
                    XmlSchemaCollectionsSource = definition.XmlSchemaCollectionsSource?.Select(selector: schema => PrepareRegisteredXmlSchema(sqlXmlSchemaCollection: schema))
            .ToArray()
                };
                definition = definition with
                {
                    ScalarTypesSource = definition.ScalarTypesSource?.Select(selector: scalar => catalogDefinitionTypes.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 15, SqlTypeContext = new SqlTypeContext { SqlTypeSource = scalar.Definition } })
            .CreateSqlTypeContextCatalogScalarTypeResult!.CatalogScalarTypeResult!)
            .ToArray()
                };
                catalogCodecContext.CatalogDefinition = definition;
                return catalogDefinitionTypes.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            case 5:
            case 6:
            case 14:
            case 15:
                return catalogDefinitionTypes.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            case 1:
            case 2:
            case 3:
            case 4:
            case 25:
            case 26:
                return catalogWireValues.ProcessCatalogCodecContext(catalogCodecContext: catalogCodecContext);
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(catalogCodecContext.Operation));
        }
    });

    internal static CatalogDefinitionTransportCoordinationService CreateCatalogDefinitionTransportCoordinationService() =>
        new CatalogDefinitionTransportCoordinationService(catalogDefinitionTypes: sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogDefinitionTypesOrchestrationService.CreateCatalogDefinitionTypesOrchestrationService(), catalogWireValues: sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogWireValuesOrchestrationService.CreateCatalogWireValuesOrchestrationService());

    private SqlXmlSchemaCollection PrepareRegisteredXmlSchema(SqlXmlSchemaCollection sqlXmlSchemaCollection)
    {
        var definitions = catalogWireValues.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 4, SqlXmlSchemaCollection = sqlXmlSchemaCollection })
            .ReadSqlXmlSchemaCollectionDefinitionsResult!;

        return catalogWireValues.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 3, SqlXmlSchemaCollection = sqlXmlSchemaCollection with { DefinitionSource = definitions } })
            .PrepareSqlXmlSchemaCollectionResult!;
    }
}