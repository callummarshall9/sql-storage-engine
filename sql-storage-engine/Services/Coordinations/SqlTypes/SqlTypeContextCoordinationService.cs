// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Orchestrations.SqlTypes;
using sql_storage_engine.Services.Orchestrations.SqlTypeValues;

namespace sql_storage_engine.Services.Coordinations.SqlTypes;
internal sealed partial class SqlTypeContextCoordinationService : ISqlTypeContextCoordinationService
{
    private readonly SqlTypeContextOrchestrationService types;
    private readonly SqlTypeValueContextOrchestrationService values;
    private readonly sql_storage_engine.Services.Orchestrations.SqlSpatials.SqlSpatialOrchestrationService spatial;
    public SqlTypeContextCoordinationService(SqlTypeContextOrchestrationService types, SqlTypeValueContextOrchestrationService values, sql_storage_engine.Services.Orchestrations.SqlSpatials.SqlSpatialOrchestrationService spatial)
    {
        this.types = types;
        this.values = values;
        this.spatial = spatial;
    }

    public SqlTypeContext ProcessSqlTypeContextCollationMetadata0(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextCollationMetadata0(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextIsUserDefined1(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextIsUserDefined1(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextQualifiedUserTypeName2(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextQualifiedUserTypeName2(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextStorageFamily3(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextStorageFamily3(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextCanBeColumn4(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextCanBeColumn4(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextCanBeBTreeKey5(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextCanBeBTreeKey5(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextCanBeSqlVariantValue6(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextCanBeSqlVariantValue6(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextBit7(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextBit7(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextTinyInt8(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextTinyInt8(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextSmallInt9(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextSmallInt9(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextInt10(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextInt10(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextBigInt11(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextBigInt11(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextSmallMoney12(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextSmallMoney12(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextMoney13(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextMoney13(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextReal14(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextReal14(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextDate15(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextDate15(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextSmallDateTime16(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextSmallDateTime16(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextDateTime17(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextDateTime17(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextUniqueIdentifier18(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextUniqueIdentifier18(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextText19(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextText19(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNText20(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNText20(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextImage21(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextImage21(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextTimestamp22(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextTimestamp22(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextRowVersion23(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextRowVersion23(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextXml24(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextXml24(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextJson25(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextJson25(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextSqlVariant26(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextSqlVariant26(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextHierarchyId27(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextHierarchyId27(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextGeometry28(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextGeometry28(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextGeography29(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextGeography29(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextCursor30(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextCursor30(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextTable31(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextTable31(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextBoolean32(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextBoolean32(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextInteger33(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextInteger33(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextDoublePrecision34(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextDoublePrecision34(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextBinaryVaryingMax35(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextBinaryVaryingMax35(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNationalText36(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNationalText36(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextSysName37(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextSysName37(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextVarBinaryMax38(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextVarBinaryMax38(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextTextWithCollation39(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextTextWithCollation39(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNTextWithCollation40(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNTextWithCollation40(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextDec41(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextDec41(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextCharacter42(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextCharacter42(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextCharacterVarying43(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextCharacterVarying43(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextCharacterVaryingMax44(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextCharacterVaryingMax44(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextBinaryVarying45(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextBinaryVarying45(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNationalCharacter46(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNationalCharacter46(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNationalChar47(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNationalChar47(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNationalCharacterVarying48(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNationalCharacterVarying48(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNationalCharVarying49(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNationalCharVarying49(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNationalCharacterVaryingMax50(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNationalCharacterVaryingMax50(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNationalCharVaryingMax51(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNationalCharVaryingMax51(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextDecimal52(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextDecimal52(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNumeric53(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNumeric53(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextFloat54(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextFloat54(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextTime55(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextTime55(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextDateTime256(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextDateTime256(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextDateTimeOffset57(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextDateTimeOffset57(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextChar58(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextChar58(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextVarChar59(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextVarChar59(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextVarCharMax60(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextVarCharMax60(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNChar61(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNChar61(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNVarChar62(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNVarChar62(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextNVarCharMax63(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNVarCharMax63(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextBinary64(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextBinary64(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextVarBinary65(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextVarBinary65(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextVector66(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextVector66(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextTypedXml67(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextTypedXml67(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextAlias68(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextAlias68(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextClrUserDefined69(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextClrUserDefined69(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextClrUserDefined70(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextClrUserDefined70(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextClrUserDefined71(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextClrUserDefined71(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextClrUserDefined72(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextClrUserDefined72(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextRestore73(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextRestore73(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextRebindXmlSchemaCollection74(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextRebindXmlSchemaCollection74(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextApplyDefaultCollation75(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextApplyDefaultCollation75(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextValidate76(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        var result = types.ProcessSqlTypeContextValidate76(sqlTypeContext: sqlTypeContext);
        return values.CheckSqlTypeContext(sqlTypeContext: result);
    });

    public SqlTypeContext ProcessSqlTypeContextNormalizeForStorage77(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextNormalizeForStorage77(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextConvert78(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlValueResult = ExecuteAssignment(target: sqlTypeContext.SqlTypeSource!, value: sqlTypeContext.InputSqlValueValue!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextConvert79(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlValueResult = ExecuteAssignment(target: sqlTypeContext.SqlTypes![0], source: sqlTypeContext.SqlTypes[1], value: sqlTypeContext.InputSqlValueValue!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextToString80(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextToString80(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext CreateSqlTypeContextCatalogScalarType(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.CreateSqlTypeContextCatalogScalarType(sqlTypeContext: sqlTypeContext);
    });

    internal static SqlTypeContextCoordinationService CreateSqlTypeContextCoordinationService() =>
        new SqlTypeContextCoordinationService(types: SqlTypeContextOrchestrationService.CreateSqlTypeContextOrchestrationService(), values: SqlTypeValueContextOrchestrationService.CreateSqlTypeValueContextOrchestrationService(), spatial: sql_storage_engine.Services.Orchestrations.SqlSpatials.SqlSpatialOrchestrationService.CreateSqlSpatialOrchestrationService());

    public SqlTypeContext ProcessSqlTypeContextDecimalCreate(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextDecimalCreate(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextDecimalParse(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return types.ProcessSqlTypeContextDecimalParse(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextHierarchyParse(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);
        return values.ProcessSqlTypeContextHierarchyParse(sqlTypeContext: sqlTypeContext);
    });

    public SqlTypeContext ProcessSqlTypeContextSpatialCreate(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SpatialResult = spatial.CreateSqlSpatial(sqlSpatial: sqlTypeContext.SpatialSource!)
        };
    });
}