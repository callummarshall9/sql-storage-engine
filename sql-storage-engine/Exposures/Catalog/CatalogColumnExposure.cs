// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogColumnExposure
{
    private static readonly sql_storage_engine.Services.Orchestrations.CatalogCodecs.ICatalogColumnAdmissionOrchestrationService service = sql_storage_engine.Services.Orchestrations.CatalogCodecs.CatalogColumnAdmissionOrchestrationService.CreateCatalogColumnAdmissionOrchestrationService();
    public static CatalogColumn CreateCatalogColumn(ColumnId columnId, string name, SqlType sqlType, bool isNullable, string? defaultExpression = null, CatalogIdentity? catalogIdentity = null, bool isRowGuidCol = false, string? computedExpression = null, bool isComputedPersisted = false, bool isSparse = false, bool isColumnSet = false, bool isFileStream = false, string? maskingFunction = null, CatalogColumnEncryption? catalogColumnEncryption = null, CatalogGeneratedAlwaysKind generatedAlways = CatalogGeneratedAlwaysKind.None, bool isHidden = false) =>
        service.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 21, CatalogColumn = new CatalogColumn { IdSource = columnId, NameSource = name, TypeSource = sqlType, IsNullableSource = isNullable, DefaultExpressionSource = defaultExpression, IdentitySource = catalogIdentity, IsRowGuidColSource = isRowGuidCol, ComputedExpressionSource = computedExpression, IsComputedPersistedSource = isComputedPersisted, IsSparseSource = isSparse, IsColumnSetSource = isColumnSet, IsFileStreamSource = isFileStream, MaskingFunctionSource = maskingFunction, EncryptionSource = catalogColumnEncryption, GeneratedAlwaysSource = generatedAlways, IsHiddenSource = isHidden } })
            .PrepareCatalogColumnResult!;

    public static CatalogColumn PrepareCatalogColumn(CatalogColumn catalogColumn) =>
        service.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 21, CatalogColumn = catalogColumn })
            .PrepareCatalogColumnResult!;
}