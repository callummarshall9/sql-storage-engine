// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Exposures.Rows;
public sealed class SqlTypeExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private static readonly sql_storage_engine.Services.Coordinations.SqlTypes.ISqlTypeContextCoordinationService service = sql_storage_engine.Services.Coordinations.SqlTypes.SqlTypeContextCoordinationService.CreateSqlTypeContextCoordinationService();
    private SqlTypeExposure()
    {
    }

    public static sql_storage_engine.Models.Rows.SqlCollation? ReadSqlTypeCollationMetadata(SqlType sqlType) =>
        service.ProcessSqlTypeContextCollationMetadata0(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType })
            .SqlCollationResult!;

    public static bool ReadSqlTypeIsUserDefined(SqlType sqlType) =>
        service.ProcessSqlTypeContextIsUserDefined1(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType })
            .BooleanResult!;

    public static string? ReadSqlTypeQualifiedUserTypeName(SqlType sqlType) =>
        service.ProcessSqlTypeContextQualifiedUserTypeName2(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType })
            .TextResult!;

    public static SqlStorageFamily ReadSqlTypeStorageFamily(SqlType sqlType) =>
        service.ProcessSqlTypeContextStorageFamily3(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType })
            .StorageFamilyResult!;

    public static bool ReadSqlTypeCanBeColumn(SqlType sqlType) =>
        service.ProcessSqlTypeContextCanBeColumn4(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType })
            .BooleanResult!;

    public static bool ReadSqlTypeCanBeBTreeKey(SqlType sqlType) =>
        service.ProcessSqlTypeContextCanBeBTreeKey5(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType })
            .BooleanResult!;

    public static bool ReadSqlTypeCanBeSqlVariantValue(SqlType sqlType) =>
        service.ProcessSqlTypeContextCanBeSqlVariantValue6(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType })
            .BooleanResult!;

    public static SqlType ReadSqlTypeBit() =>
        service.ProcessSqlTypeContextBit7(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeTinyInt() =>
        service.ProcessSqlTypeContextTinyInt8(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeSmallInt() =>
        service.ProcessSqlTypeContextSmallInt9(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeInt() =>
        service.ProcessSqlTypeContextInt10(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeBigInt() =>
        service.ProcessSqlTypeContextBigInt11(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeSmallMoney() =>
        service.ProcessSqlTypeContextSmallMoney12(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeMoney() =>
        service.ProcessSqlTypeContextMoney13(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeReal() =>
        service.ProcessSqlTypeContextReal14(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeDate() =>
        service.ProcessSqlTypeContextDate15(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeSmallDateTime() =>
        service.ProcessSqlTypeContextSmallDateTime16(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeDateTime() =>
        service.ProcessSqlTypeContextDateTime17(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeUniqueIdentifier() =>
        service.ProcessSqlTypeContextUniqueIdentifier18(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeText() =>
        service.ProcessSqlTypeContextText19(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeNText() =>
        service.ProcessSqlTypeContextNText20(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeImage() =>
        service.ProcessSqlTypeContextImage21(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeTimestamp() =>
        service.ProcessSqlTypeContextTimestamp22(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeRowVersion() =>
        service.ProcessSqlTypeContextRowVersion23(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeXml() =>
        service.ProcessSqlTypeContextXml24(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeJson() =>
        service.ProcessSqlTypeContextJson25(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeSqlVariant() =>
        service.ProcessSqlTypeContextSqlVariant26(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeHierarchyId() =>
        service.ProcessSqlTypeContextHierarchyId27(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeGeometry() =>
        service.ProcessSqlTypeContextGeometry28(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeGeography() =>
        service.ProcessSqlTypeContextGeography29(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeCursor() =>
        service.ProcessSqlTypeContextCursor30(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeTable() =>
        service.ProcessSqlTypeContextTable31(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeBoolean() =>
        service.ProcessSqlTypeContextBoolean32(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeInteger() =>
        service.ProcessSqlTypeContextInteger33(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeDoublePrecision() =>
        service.ProcessSqlTypeContextDoublePrecision34(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeBinaryVaryingMax() =>
        service.ProcessSqlTypeContextBinaryVaryingMax35(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeNationalText() =>
        service.ProcessSqlTypeContextNationalText36(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeSysName() =>
        service.ProcessSqlTypeContextSysName37(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType ReadSqlTypeVarBinaryMax() =>
        service.ProcessSqlTypeContextVarBinaryMax38(sqlTypeContext: new SqlTypeContext { })
            .SqlTypeResult!;

    public static SqlType CreateTextWithCollationSqlType(string collation) =>
        service.ProcessSqlTypeContextTextWithCollation39(sqlTypeContext: new SqlTypeContext { InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateNTextWithCollationSqlType(string collation) =>
        service.ProcessSqlTypeContextNTextWithCollation40(sqlTypeContext: new SqlTypeContext { InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateDecSqlType(byte precision = 18, byte scale = 0) =>
        service.ProcessSqlTypeContextDec41(sqlTypeContext: new SqlTypeContext { InputbytePrecision = precision, InputbyteScale = scale })
            .SqlTypeResult!;

    public static SqlType CreateCharacterSqlType(int length = 1, string? collation = null) =>
        service.ProcessSqlTypeContextCharacter42(sqlTypeContext: new SqlTypeContext { InputintLength = length, InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateCharacterVaryingSqlType(int length = 1, string? collation = null) =>
        service.ProcessSqlTypeContextCharacterVarying43(sqlTypeContext: new SqlTypeContext { InputintLength = length, InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateCharacterVaryingMaxSqlType(string? collation = null) =>
        service.ProcessSqlTypeContextCharacterVaryingMax44(sqlTypeContext: new SqlTypeContext { InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateBinaryVaryingSqlType(int length = 1) =>
        service.ProcessSqlTypeContextBinaryVarying45(sqlTypeContext: new SqlTypeContext { InputintLength = length })
            .SqlTypeResult!;

    public static SqlType CreateNationalCharacterSqlType(int length = 1, string? collation = null) =>
        service.ProcessSqlTypeContextNationalCharacter46(sqlTypeContext: new SqlTypeContext { InputintLength = length, InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateNationalCharSqlType(int length = 1, string? collation = null) =>
        service.ProcessSqlTypeContextNationalChar47(sqlTypeContext: new SqlTypeContext { InputintLength = length, InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateNationalCharacterVaryingSqlType(int length = 1, string? collation = null) =>
        service.ProcessSqlTypeContextNationalCharacterVarying48(sqlTypeContext: new SqlTypeContext { InputintLength = length, InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateNationalCharVaryingSqlType(int length = 1, string? collation = null) =>
        service.ProcessSqlTypeContextNationalCharVarying49(sqlTypeContext: new SqlTypeContext { InputintLength = length, InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateNationalCharacterVaryingMaxSqlType(string? collation = null) =>
        service.ProcessSqlTypeContextNationalCharacterVaryingMax50(sqlTypeContext: new SqlTypeContext { InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateNationalCharVaryingMaxSqlType(string? collation = null) =>
        service.ProcessSqlTypeContextNationalCharVaryingMax51(sqlTypeContext: new SqlTypeContext { InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateDecimalSqlType(byte precision = 18, byte scale = 0) =>
        service.ProcessSqlTypeContextDecimal52(sqlTypeContext: new SqlTypeContext { InputbytePrecision = precision, InputbyteScale = scale })
            .SqlTypeResult!;

    public static SqlType CreateNumericSqlType(byte precision = 18, byte scale = 0) =>
        service.ProcessSqlTypeContextNumeric53(sqlTypeContext: new SqlTypeContext { InputbytePrecision = precision, InputbyteScale = scale })
            .SqlTypeResult!;

    public static SqlType CreateFloatSqlType(byte precision = 53) =>
        service.ProcessSqlTypeContextFloat54(sqlTypeContext: new SqlTypeContext { InputbytePrecision = precision })
            .SqlTypeResult!;

    public static SqlType CreateTimeSqlType(byte scale = 7) =>
        service.ProcessSqlTypeContextTime55(sqlTypeContext: new SqlTypeContext { InputbyteScale = scale })
            .SqlTypeResult!;

    public static SqlType CreateDateTime2SqlType(byte scale = 7) =>
        service.ProcessSqlTypeContextDateTime256(sqlTypeContext: new SqlTypeContext { InputbyteScale = scale })
            .SqlTypeResult!;

    public static SqlType CreateDateTimeOffsetSqlType(byte scale = 7) =>
        service.ProcessSqlTypeContextDateTimeOffset57(sqlTypeContext: new SqlTypeContext { InputbyteScale = scale })
            .SqlTypeResult!;

    public static SqlType CreateCharSqlType(int length = 1, string? collation = null) =>
        service.ProcessSqlTypeContextChar58(sqlTypeContext: new SqlTypeContext { InputintLength = length, InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateVarCharSqlType(int length = 1, string? collation = null) =>
        service.ProcessSqlTypeContextVarChar59(sqlTypeContext: new SqlTypeContext { InputintLength = length, InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateVarCharMaxSqlType(string? collation = null) =>
        service.ProcessSqlTypeContextVarCharMax60(sqlTypeContext: new SqlTypeContext { InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateNCharSqlType(int length = 1, string? collation = null) =>
        service.ProcessSqlTypeContextNChar61(sqlTypeContext: new SqlTypeContext { InputintLength = length, InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateNVarCharSqlType(int length = 1, string? collation = null) =>
        service.ProcessSqlTypeContextNVarChar62(sqlTypeContext: new SqlTypeContext { InputintLength = length, InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateNVarCharMaxSqlType(string? collation = null) =>
        service.ProcessSqlTypeContextNVarCharMax63(sqlTypeContext: new SqlTypeContext { InputstringCollation = collation })
            .SqlTypeResult!;

    public static SqlType CreateBinarySqlType(int length = 1) =>
        service.ProcessSqlTypeContextBinary64(sqlTypeContext: new SqlTypeContext { InputintLength = length })
            .SqlTypeResult!;

    public static SqlType CreateVarBinarySqlType(int length = 1) =>
        service.ProcessSqlTypeContextVarBinary65(sqlTypeContext: new SqlTypeContext { InputintLength = length })
            .SqlTypeResult!;

    public static SqlType CreateVectorSqlType(ushort dimensions, SqlVectorBaseType baseType = SqlVectorBaseType.Float32) =>
        service.ProcessSqlTypeContextVector66(sqlTypeContext: new SqlTypeContext { InputushortDimensions = dimensions, InputSqlVectorBaseTypeBaseType = baseType })
            .SqlTypeResult!;

    public static SqlType CreateTypedXmlSqlType(SqlXmlSchemaCollection sqlXmlSchemaCollection, SqlXmlContentKind contentKind = SqlXmlContentKind.Content) =>
        service.ProcessSqlTypeContextTypedXml67(sqlTypeContext: new SqlTypeContext { InputSqlXmlSchemaCollectionSchemaCollection = sqlXmlSchemaCollection, InputSqlXmlContentKindContentKind = contentKind })
            .SqlTypeResult!;

    public static SqlType CreateAliasSqlType(string schemaName, string name, SqlType sqlType, bool isNullable = true) =>
        service.ProcessSqlTypeContextAlias68(sqlTypeContext: new SqlTypeContext { InputstringSchemaName = schemaName, InputstringName = name, InputSqlTypeBaseType = sqlType, InputboolIsNullable = isNullable })
            .SqlTypeResult!;

    public static SqlType CreateClrUserDefinedSqlType(string schemaName, string name, string assemblyName, string className, SqlClrSerializationFormat serializationFormat, int maxByteSize, bool isByteOrdered = false, bool isFixedLength = false, string? validationMethodName = null) =>
        service.ProcessSqlTypeContextClrUserDefined69(sqlTypeContext: new SqlTypeContext { InputstringSchemaName = schemaName, InputstringName = name, InputstringAssemblyName = assemblyName, InputstringClassName = className, InputSqlClrSerializationFormatSerializationFormat = serializationFormat, InputintMaxByteSize = maxByteSize, InputboolIsByteOrdered = isByteOrdered, InputboolIsFixedLength = isFixedLength, InputstringValidationMethodName = validationMethodName })
            .SqlTypeResult!;

    public static SqlType CreateClrUserDefinedSqlType(string schemaName, string name, string assemblyName, string className, SqlClrSerializationFormat serializationFormat, bool isByteOrdered = false, bool isFixedLength = false, string? validationMethodName = null) =>
        service.ProcessSqlTypeContextClrUserDefined70(sqlTypeContext: new SqlTypeContext { InputstringSchemaName = schemaName, InputstringName = name, InputstringAssemblyName = assemblyName, InputstringClassName = className, InputSqlClrSerializationFormatSerializationFormat = serializationFormat, InputboolIsByteOrdered = isByteOrdered, InputboolIsFixedLength = isFixedLength, InputstringValidationMethodName = validationMethodName })
            .SqlTypeResult!;

    public static SqlType CreateClrUserDefinedSqlType(string schemaName, string name, string assemblyName, SqlClrSerializationFormat serializationFormat, int maxByteSize, bool isByteOrdered = false, bool isFixedLength = false, string? validationMethodName = null) =>
        service.ProcessSqlTypeContextClrUserDefined71(sqlTypeContext: new SqlTypeContext { InputstringSchemaName = schemaName, InputstringName = name, InputstringAssemblyName = assemblyName, InputSqlClrSerializationFormatSerializationFormat = serializationFormat, InputintMaxByteSize = maxByteSize, InputboolIsByteOrdered = isByteOrdered, InputboolIsFixedLength = isFixedLength, InputstringValidationMethodName = validationMethodName })
            .SqlTypeResult!;

    public static SqlType CreateClrUserDefinedSqlType(string schemaName, string name, string assemblyName, SqlClrSerializationFormat serializationFormat, bool isByteOrdered = false, bool isFixedLength = false, string? validationMethodName = null) =>
        service.ProcessSqlTypeContextClrUserDefined72(sqlTypeContext: new SqlTypeContext { InputstringSchemaName = schemaName, InputstringName = name, InputstringAssemblyName = assemblyName, InputSqlClrSerializationFormatSerializationFormat = serializationFormat, InputboolIsByteOrdered = isByteOrdered, InputboolIsFixedLength = isFixedLength, InputstringValidationMethodName = validationMethodName })
            .SqlTypeResult!;

    internal static SqlType CreateRestoreSqlType(SqlTypeName name, byte? precision, byte? scale, int? length, bool isMax, string? collation, ushort? vectorDimensions, SqlVectorBaseType? vectorBaseType, SqlXmlContentKind? xmlContentKind = null, SqlXmlSchemaCollection? sqlXmlSchemaCollection = null, SqlUserTypeKind userTypeKind = SqlUserTypeKind.None, string? userTypeSchema = null, string? userTypeName = null, SqlType? sqlType = null, bool? userTypeNullable = null, string? clrAssemblyName = null, string? clrClassName = null, SqlClrSerializationFormat? clrSerializationFormat = null, int? clrMaxByteSize = null, bool clrIsByteOrdered = false, bool clrIsFixedLength = false, string? clrValidationMethodName = null) =>
        service.ProcessSqlTypeContextRestore73(sqlTypeContext: new SqlTypeContext { InputSqlTypeNameName = name, InputbytePrecision = precision, InputbyteScale = scale, InputintLength = length, InputboolIsMax = isMax, InputstringCollation = collation, InputushortVectorDimensions = vectorDimensions, InputSqlVectorBaseTypeVectorBaseType = vectorBaseType, InputSqlXmlContentKindXmlContentKind = xmlContentKind, InputSqlXmlSchemaCollectionXmlSchemaCollection = sqlXmlSchemaCollection, InputSqlUserTypeKindUserTypeKind = userTypeKind, InputstringUserTypeSchema = userTypeSchema, InputstringUserTypeName = userTypeName, InputSqlTypeBaseType = sqlType, InputboolUserTypeNullable = userTypeNullable, InputstringClrAssemblyName = clrAssemblyName, InputstringClrClassName = clrClassName, InputSqlClrSerializationFormatClrSerializationFormat = clrSerializationFormat, InputintClrMaxByteSize = clrMaxByteSize, InputboolClrIsByteOrdered = clrIsByteOrdered, InputboolClrIsFixedLength = clrIsFixedLength, InputstringClrValidationMethodName = clrValidationMethodName })
            .SqlTypeResult!;

    internal static SqlType RebindSqlTypeXmlSchemaCollection(SqlType sqlType, SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        service.ProcessSqlTypeContextRebindXmlSchemaCollection74(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType, InputSqlXmlSchemaCollectionCollection = sqlXmlSchemaCollection })
            .SqlTypeResult!;

    internal static SqlType ApplySqlTypeDefaultCollation(SqlType sqlType, string defaultCollation) =>
        service.ProcessSqlTypeContextApplyDefaultCollation75(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType, InputstringDefaultCollation = defaultCollation })
            .SqlTypeResult!;

    public static void CheckSqlTypeValue(SqlType sqlType, SqlValue sqlValue, string columnName) =>
        service.ProcessSqlTypeContextValidate76(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType, InputSqlValueValue = sqlValue, InputstringColumnName = columnName });

    internal static SqlValue NormalizeSqlTypeValue(SqlType sqlType, SqlValue sqlValue) =>
        service.ProcessSqlTypeContextNormalizeForStorage77(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType, InputSqlValueValue = sqlValue })
            .SqlValueResult!;

    public static SqlValue ConvertSqlTypeValue(SqlType sqlType, SqlValue sqlValue) =>
        service.ProcessSqlTypeContextConvert78(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType, InputSqlValueValue = sqlValue })
            .SqlValueResult!;

    public static SqlValue ConvertSqlTypeValue(SqlType[] sqlTypes, SqlValue sqlValue) =>
        service.ProcessSqlTypeContextConvert79(sqlTypeContext: new SqlTypeContext { SqlTypes = sqlTypes, InputSqlValueValue = sqlValue })
            .SqlValueResult!;

    public static string FormatSqlType(SqlType sqlType) =>
        service.ProcessSqlTypeContextToString80(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType })
            .TextResult!;

    public static sql_storage_engine.Models.Catalog.CatalogScalarType CreateCatalogScalarType(SqlType sqlType) =>
        service.CreateSqlTypeContextCatalogScalarType(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType })
            .CatalogScalarTypeResult!;
}