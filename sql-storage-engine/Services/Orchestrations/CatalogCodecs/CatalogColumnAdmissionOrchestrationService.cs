// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.CatalogCodecs;
internal sealed partial class CatalogColumnAdmissionOrchestrationService : ICatalogColumnAdmissionOrchestrationService
{
    private readonly sql_storage_engine.Services.Processings.CatalogMetadata.SqlCollationCatalogColumnProcessingService columns;
    private readonly sql_storage_engine.Services.Processings.CatalogMasks.SqlMaskFunctionProcessingService maskFunctions;
    private readonly sql_storage_engine.Services.Processings.CatalogMasks.SqlMaskAdmissionProcessingService maskAdmissions;
    private CatalogColumnAdmissionOrchestrationService(sql_storage_engine.Services.Processings.CatalogMetadata.SqlCollationCatalogColumnProcessingService columns, sql_storage_engine.Services.Processings.CatalogMasks.SqlMaskFunctionProcessingService maskFunctions, sql_storage_engine.Services.Processings.CatalogMasks.SqlMaskAdmissionProcessingService maskAdmissions)
    {
        this.columns = columns;
        this.maskFunctions = maskFunctions;
        this.maskAdmissions = maskAdmissions;
    }

    internal static CatalogColumnAdmissionOrchestrationService CreateCatalogColumnAdmissionOrchestrationService() =>
        new CatalogColumnAdmissionOrchestrationService(columns: sql_storage_engine.Services.Processings.CatalogMetadata.SqlCollationCatalogColumnProcessingService.CreateSqlCollationCatalogColumnProcessingService(), maskFunctions: new sql_storage_engine.Services.Processings.CatalogMasks.SqlMaskFunctionProcessingService(), maskAdmissions: new sql_storage_engine.Services.Processings.CatalogMasks.SqlMaskAdmissionProcessingService());

    public CatalogCodecContext ProcessCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogCodecContext(catalogCodecContext: catalogCodecContext);

        switch (catalogCodecContext.Operation)
        {
            case 19:
                ValidateCatalogIdentity(catalogIdentity: catalogCodecContext.CatalogIdentity!);
                catalogCodecContext.PrepareCatalogIdentityResult = catalogCodecContext.CatalogIdentity;
                return catalogCodecContext;
            case 20:
                ValidateCatalogColumnEncryption(catalogColumnEncryption: catalogCodecContext.CatalogColumnEncryption!);
                catalogCodecContext.PrepareCatalogColumnEncryptionResult = catalogCodecContext.CatalogColumnEncryption;
                return catalogCodecContext;
            case 21:
                var column = columns.PrepareCatalogColumn(catalogColumn: catalogCodecContext.CatalogColumn!);
                if (column.MaskingFunction is not null)
                {
                    var mask = maskFunctions.ReadSqlMaskContext(sqlMaskContext: new SqlMaskContext { Type = column.Type, Function = column.MaskingFunction });
                    maskAdmissions.CheckSqlMaskContext(sqlMaskContext: mask);
                }

                catalogCodecContext.PrepareCatalogColumnResult = column;
                return catalogCodecContext;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(catalogCodecContext.Operation));
        }
    });
}