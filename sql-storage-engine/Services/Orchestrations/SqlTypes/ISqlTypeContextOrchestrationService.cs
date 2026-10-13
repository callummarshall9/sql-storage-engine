// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlTypes;
internal interface ISqlTypeContextOrchestrationService
{
    SqlTypeContext ProcessSqlTypeContextCollationMetadata0(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextIsUserDefined1(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextQualifiedUserTypeName2(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextStorageFamily3(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextCanBeColumn4(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextCanBeBTreeKey5(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextCanBeSqlVariantValue6(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextBit7(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextTinyInt8(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextSmallInt9(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextInt10(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextBigInt11(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextSmallMoney12(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextMoney13(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextReal14(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextDate15(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextSmallDateTime16(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextDateTime17(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextUniqueIdentifier18(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextText19(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNText20(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextImage21(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextTimestamp22(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextRowVersion23(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextXml24(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextJson25(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextSqlVariant26(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextHierarchyId27(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextGeometry28(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextGeography29(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextCursor30(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextTable31(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextBoolean32(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextInteger33(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextDoublePrecision34(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextBinaryVaryingMax35(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNationalText36(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextSysName37(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextVarBinaryMax38(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextTextWithCollation39(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNTextWithCollation40(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextDec41(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextCharacter42(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextCharacterVarying43(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextCharacterVaryingMax44(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextBinaryVarying45(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNationalCharacter46(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNationalChar47(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNationalCharacterVarying48(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNationalCharVarying49(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNationalCharacterVaryingMax50(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNationalCharVaryingMax51(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextDecimal52(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNumeric53(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextFloat54(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextTime55(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextDateTime256(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextDateTimeOffset57(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextChar58(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextVarChar59(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextVarCharMax60(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNChar61(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNVarChar62(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNVarCharMax63(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextBinary64(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextVarBinary65(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextVector66(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextTypedXml67(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextAlias68(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextClrUserDefined69(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextClrUserDefined70(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextClrUserDefined71(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextClrUserDefined72(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextRestore73(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextRebindXmlSchemaCollection74(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextApplyDefaultCollation75(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextValidate76(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextNormalizeForStorage77(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextToString80(SqlTypeContext sqlTypeContext);

    SqlTypeContext CreateSqlTypeContextCatalogScalarType(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextDecimalParse(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextDecimalCreate(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextDecimalFormat(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextDecimalRound(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextVectorParse(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextVectorFormat(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextUtf8(SqlTypeContext sqlTypeContext);
}