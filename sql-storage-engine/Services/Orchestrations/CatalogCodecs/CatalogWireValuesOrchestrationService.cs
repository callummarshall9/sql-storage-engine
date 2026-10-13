// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogCodecs;
internal sealed partial class CatalogWireValuesOrchestrationService : ICatalogWireValuesOrchestrationService
{
    private readonly sql_storage_engine.Services.Processings.CatalogCodecs.SqlXmlSchemaCollectionCatalogCodecProcessingService schemas;
    private readonly sql_storage_engine.Services.Processings.CatalogCodecs.SecurityStateCatalogCodecProcessingService securityStates;
    private readonly sql_storage_engine.Services.Processings.CatalogCodecs.SqlCollationCatalogTextEncodingProcessingService textEncoding;
    public CatalogWireValuesOrchestrationService(sql_storage_engine.Services.Processings.CatalogCodecs.SqlXmlSchemaCollectionCatalogCodecProcessingService schemas, sql_storage_engine.Services.Processings.CatalogCodecs.SecurityStateCatalogCodecProcessingService securityStates, sql_storage_engine.Services.Processings.CatalogCodecs.SqlCollationCatalogTextEncodingProcessingService textEncoding)
    {
        this.schemas = schemas;
        this.securityStates = securityStates;
        this.textEncoding = textEncoding;
    }

    public CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogCodecContext(catalogCodecContext: catalogCodecContext);

        switch (catalogCodecContext.Operation)
        {
            case 1:
                catalogCodecContext.EncodeSecurityStatePayloadResult = securityStates.EncodeSecurityStatePayload(securityStatePayload: catalogCodecContext.SecurityStatePayload!);
                return catalogCodecContext;
            case 2:
                catalogCodecContext.DecodeSecurityStatePayloadResult = securityStates.DecodeSecurityStatePayload(securityStatePayload: catalogCodecContext.SecurityStatePayload!);
                return catalogCodecContext;
            case 3:
                catalogCodecContext.PrepareSqlXmlSchemaCollectionResult = schemas.PrepareSqlXmlSchemaCollection(sqlXmlSchemaCollection: catalogCodecContext.SqlXmlSchemaCollection!);
                return catalogCodecContext;
            case 4:
                catalogCodecContext.ReadSqlXmlSchemaCollectionDefinitionsResult = schemas.ReadSqlXmlSchemaCollectionDefinitions(sqlXmlSchemaCollection: catalogCodecContext.SqlXmlSchemaCollection!);
                return catalogCodecContext;
            case 25:
                catalogCodecContext.EncodeCatalogTextEncodingContextResult = textEncoding.EncodeCatalogTextEncodingContext(catalogTextEncodingContext: catalogCodecContext.CatalogTextEncodingContext!);
                return catalogCodecContext;
            case 26:
                catalogCodecContext.DecodeCatalogTextEncodingContextResult = textEncoding.DecodeCatalogTextEncodingContext(catalogTextEncodingContext: catalogCodecContext.CatalogTextEncodingContext!);
                return catalogCodecContext;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(catalogCodecContext.Operation));
        }
    });

    internal static CatalogWireValuesOrchestrationService CreateCatalogWireValuesOrchestrationService() =>
        new CatalogWireValuesOrchestrationService(schemas: sql_storage_engine.Services.Processings.CatalogCodecs.SqlXmlSchemaCollectionCatalogCodecProcessingService.CreateSqlXmlSchemaCollectionCatalogCodecProcessingService(), securityStates: sql_storage_engine.Services.Processings.CatalogCodecs.SecurityStateCatalogCodecProcessingService.CreateSecurityStateCatalogCodecProcessingService(), textEncoding: sql_storage_engine.Services.Processings.CatalogCodecs.SqlCollationCatalogTextEncodingProcessingService.CreateSqlCollationCatalogTextEncodingProcessingService());
}