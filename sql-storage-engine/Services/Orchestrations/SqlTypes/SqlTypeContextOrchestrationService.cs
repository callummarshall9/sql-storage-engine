// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Globalization;
using System.Numerics;
using System.Text;
using System;
using sql_storage_engine.Services.Foundations.SqlCollations;
using sql_storage_engine.Services.Foundations.SqlTypeDocuments;
using sql_storage_engine.Services.Foundations.SqlDecimals;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.SqlTypes;
internal sealed partial class SqlTypeContextOrchestrationService : ISqlTypeContextOrchestrationService
{
    public SqlTypeContextOrchestrationService(SqlCollationService sqlCollationService, SqlTypeDocumentService sqlTypeDocumentService, SqlDecimalService sqlDecimalService)
    {
        collations = sqlCollationService;
        documents = sqlTypeDocumentService;
        decimals = sqlDecimalService;
        sqlTypeValueBit = new SqlType
        {
            Name = SqlTypeName.Bit
        };
        sqlTypeValueTinyInt = new SqlType
        {
            Name = SqlTypeName.TinyInt
        };
        sqlTypeValueSmallInt = new SqlType
        {
            Name = SqlTypeName.SmallInt
        };
        sqlTypeValueInt = new SqlType
        {
            Name = SqlTypeName.Int
        };
        sqlTypeValueBigInt = new SqlType
        {
            Name = SqlTypeName.BigInt
        };
        sqlTypeValueSmallMoney = new SqlType
        {
            Name = SqlTypeName.SmallMoney,
            Precision = 10,
            Scale = 4
        };
        sqlTypeValueMoney = new SqlType
        {
            Name = SqlTypeName.Money,
            Precision = 19,
            Scale = 4
        };
        sqlTypeValueReal = new SqlType
        {
            Name = SqlTypeName.Real,
            Precision = 24
        };
        sqlTypeValueDate = new SqlType
        {
            Name = SqlTypeName.Date
        };
        sqlTypeValueSmallDateTime = new SqlType
        {
            Name = SqlTypeName.SmallDateTime,
            Scale = 0
        };
        sqlTypeValueDateTime = new SqlType
        {
            Name = SqlTypeName.DateTime,
            Scale = 3
        };
        sqlTypeValueUniqueIdentifier = new SqlType
        {
            Name = SqlTypeName.UniqueIdentifier
        };
        sqlTypeValueText = ExecuteCharSqlType(name: SqlTypeName.Text, length: null, isMax: true, collation: null);
        sqlTypeValueNText = ExecuteCharSqlType(name: SqlTypeName.NText, length: null, isMax: true, collation: null);
        sqlTypeValueImage = new SqlType
        {
            Name = SqlTypeName.Image,
            IsMax = true
        };
        sqlTypeValueTimestamp = new SqlType
        {
            Name = SqlTypeName.Timestamp,
            Length = 8
        };
        sqlTypeValueXml = new SqlType
        {
            Name = SqlTypeName.Xml,
            IsMax = true
        };
        sqlTypeValueJson = new SqlType
        {
            Name = SqlTypeName.Json,
            IsMax = true
        };
        sqlTypeValueSqlVariant = new SqlType
        {
            Name = SqlTypeName.SqlVariant
        };
        sqlTypeValueHierarchyId = new SqlType
        {
            Name = SqlTypeName.HierarchyId,
            IsMax = true
        };
        sqlTypeValueGeometry = new SqlType
        {
            Name = SqlTypeName.Geometry,
            IsMax = true
        };
        sqlTypeValueGeography = new SqlType
        {
            Name = SqlTypeName.Geography,
            IsMax = true
        };
        sqlTypeValueCursor = new SqlType
        {
            Name = SqlTypeName.Cursor
        };
        sqlTypeValueTable = new SqlType
        {
            Name = SqlTypeName.Table
        };
        sqlTypeValueVarBinaryMax = new SqlType
        {
            Name = SqlTypeName.VarBinary,
            IsMax = true
        };
    }

    private readonly SqlCollationService collations;
    private readonly SqlTypeDocumentService documents;
    private readonly SqlDecimalService decimals;
    public SqlTypeContext ProcessSqlTypeContextCollationMetadata0(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSource(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlCollationResult = ReadCollationMetadataSqlType(sqlType: sqlTypeContext.SqlTypeSource!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextIsUserDefined1(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSource(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            BooleanResult = ReadIsUserDefinedSqlType(sqlType: sqlTypeContext.SqlTypeSource!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextQualifiedUserTypeName2(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSource(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            TextResult = ReadQualifiedUserTypeNameSqlType(sqlType: sqlTypeContext.SqlTypeSource!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextStorageFamily3(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSource(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            StorageFamilyResult = ReadStorageFamilySqlType(sqlType: sqlTypeContext.SqlTypeSource!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextCanBeColumn4(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSource(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            BooleanResult = ReadCanBeColumnSqlType(sqlType: sqlTypeContext.SqlTypeSource!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextCanBeBTreeKey5(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSource(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            BooleanResult = ReadCanBeBTreeKeySqlType(sqlType: sqlTypeContext.SqlTypeSource!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextCanBeSqlVariantValue6(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSource(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            BooleanResult = ReadCanBeSqlVariantValueSqlType(sqlType: sqlTypeContext.SqlTypeSource!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextBit7(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueBit
        };
    });

    public SqlTypeContext ProcessSqlTypeContextTinyInt8(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueTinyInt
        };
    });

    public SqlTypeContext ProcessSqlTypeContextSmallInt9(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueSmallInt
        };
    });

    public SqlTypeContext ProcessSqlTypeContextInt10(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueInt
        };
    });

    public SqlTypeContext ProcessSqlTypeContextBigInt11(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueBigInt
        };
    });

    public SqlTypeContext ProcessSqlTypeContextSmallMoney12(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueSmallMoney
        };
    });

    public SqlTypeContext ProcessSqlTypeContextMoney13(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueMoney
        };
    });

    public SqlTypeContext ProcessSqlTypeContextReal14(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueReal
        };
    });

    public SqlTypeContext ProcessSqlTypeContextDate15(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueDate
        };
    });

    public SqlTypeContext ProcessSqlTypeContextSmallDateTime16(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueSmallDateTime
        };
    });

    public SqlTypeContext ProcessSqlTypeContextDateTime17(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueDateTime
        };
    });

    public SqlTypeContext ProcessSqlTypeContextUniqueIdentifier18(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueUniqueIdentifier
        };
    });

    public SqlTypeContext ProcessSqlTypeContextText19(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueText
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNText20(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueNText
        };
    });

    public SqlTypeContext ProcessSqlTypeContextImage21(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueImage
        };
    });

    public SqlTypeContext ProcessSqlTypeContextTimestamp22(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueTimestamp
        };
    });

    public SqlTypeContext ProcessSqlTypeContextRowVersion23(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueTimestamp
        };
    });

    public SqlTypeContext ProcessSqlTypeContextXml24(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueXml
        };
    });

    public SqlTypeContext ProcessSqlTypeContextJson25(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueJson
        };
    });

    public SqlTypeContext ProcessSqlTypeContextSqlVariant26(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueSqlVariant
        };
    });

    public SqlTypeContext ProcessSqlTypeContextHierarchyId27(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueHierarchyId
        };
    });

    public SqlTypeContext ProcessSqlTypeContextGeometry28(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueGeometry
        };
    });

    public SqlTypeContext ProcessSqlTypeContextGeography29(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueGeography
        };
    });

    public SqlTypeContext ProcessSqlTypeContextCursor30(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueCursor
        };
    });

    public SqlTypeContext ProcessSqlTypeContextTable31(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueTable
        };
    });

    public SqlTypeContext ProcessSqlTypeContextBoolean32(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueBit
        };
    });

    public SqlTypeContext ProcessSqlTypeContextInteger33(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueInt
        };
    });

    public SqlTypeContext ProcessSqlTypeContextDoublePrecision34(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteFloatSqlType(precision: 53)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextBinaryVaryingMax35(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueVarBinaryMax
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNationalText36(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueNText
        };
    });

    public SqlTypeContext ProcessSqlTypeContextSysName37(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteNVarCharSqlType(length: 128)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextVarBinaryMax38(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = this.sqlTypeValueVarBinaryMax
        };
    });

    public SqlTypeContext ProcessSqlTypeContextTextWithCollation39(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteTextWithCollationSqlType(collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNTextWithCollation40(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteNTextWithCollationSqlType(collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextDec41(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteDecimalSqlType(precision: sqlTypeContext.InputbytePrecision!.Value, scale: sqlTypeContext.InputbyteScale!.Value)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextCharacter42(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteCharSqlType(length: sqlTypeContext.InputintLength!.Value, collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextCharacterVarying43(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteVarCharSqlType(length: sqlTypeContext.InputintLength!.Value, collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextCharacterVaryingMax44(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteVarCharMaxSqlType(collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextBinaryVarying45(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteVarBinarySqlType(length: sqlTypeContext.InputintLength!.Value)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNationalCharacter46(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteNCharSqlType(length: sqlTypeContext.InputintLength!.Value, collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNationalChar47(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteNCharSqlType(length: sqlTypeContext.InputintLength!.Value, collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNationalCharacterVarying48(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteNVarCharSqlType(length: sqlTypeContext.InputintLength!.Value, collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNationalCharVarying49(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteNVarCharSqlType(length: sqlTypeContext.InputintLength!.Value, collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNationalCharacterVaryingMax50(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteNVarCharMaxSqlType(collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNationalCharVaryingMax51(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteNVarCharMaxSqlType(collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextDecimal52(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteDecimalSqlType(precision: sqlTypeContext.InputbytePrecision!.Value, scale: sqlTypeContext.InputbyteScale!.Value)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNumeric53(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteNumericSqlType(precision: sqlTypeContext.InputbytePrecision!.Value, scale: sqlTypeContext.InputbyteScale!.Value)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextFloat54(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteFloatSqlType(precision: sqlTypeContext.InputbytePrecision!.Value)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextTime55(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteTimeSqlType(scale: sqlTypeContext.InputbyteScale!.Value)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextDateTime256(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteDateTime2SqlType(scale: sqlTypeContext.InputbyteScale!.Value)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextDateTimeOffset57(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteDateTimeOffsetSqlType(scale: sqlTypeContext.InputbyteScale!.Value)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextChar58(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteCharSqlType(length: sqlTypeContext.InputintLength!.Value, collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextVarChar59(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteVarCharSqlType(length: sqlTypeContext.InputintLength!.Value, collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextVarCharMax60(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteVarCharMaxSqlType(collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNChar61(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteNCharSqlType(length: sqlTypeContext.InputintLength!.Value, collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNVarChar62(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteNVarCharSqlType(length: sqlTypeContext.InputintLength!.Value, collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextNVarCharMax63(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteNVarCharMaxSqlType(collation: sqlTypeContext.InputstringCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextBinary64(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteBinarySqlType(length: sqlTypeContext.InputintLength!.Value)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextVarBinary65(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteVarBinarySqlType(length: sqlTypeContext.InputintLength!.Value)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextVector66(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteVectorSqlType(dimensions: sqlTypeContext.InputushortDimensions!, baseType: sqlTypeContext.InputSqlVectorBaseTypeBaseType!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextTypedXml67(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteTypedXmlSqlType(schemaCollection: sqlTypeContext.InputSqlXmlSchemaCollectionSchemaCollection!, contentKind: sqlTypeContext.InputSqlXmlContentKindContentKind!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextAlias68(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteAliasSqlType(schemaName: sqlTypeContext.InputstringSchemaName!, name: sqlTypeContext.InputstringName!, baseType: sqlTypeContext.InputSqlTypeBaseType!, isNullable: sqlTypeContext.InputboolIsNullable!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextClrUserDefined69(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteClrUserDefinedSqlType(schemaName: sqlTypeContext.InputstringSchemaName!, name: sqlTypeContext.InputstringName!, assemblyName: sqlTypeContext.InputstringAssemblyName!, className: sqlTypeContext.InputstringClassName!, serializationFormat: sqlTypeContext.InputSqlClrSerializationFormatSerializationFormat!, maxByteSize: sqlTypeContext.InputintMaxByteSize!, isByteOrdered: sqlTypeContext.InputboolIsByteOrdered!, isFixedLength: sqlTypeContext.InputboolIsFixedLength!, validationMethodName: sqlTypeContext.InputstringValidationMethodName!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextClrUserDefined70(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteClrUserDefinedSqlType(schemaName: sqlTypeContext.InputstringSchemaName!, name: sqlTypeContext.InputstringName!, assemblyName: sqlTypeContext.InputstringAssemblyName!, className: sqlTypeContext.InputstringClassName!, serializationFormat: sqlTypeContext.InputSqlClrSerializationFormatSerializationFormat!, isByteOrdered: sqlTypeContext.InputboolIsByteOrdered!, isFixedLength: sqlTypeContext.InputboolIsFixedLength!, validationMethodName: sqlTypeContext.InputstringValidationMethodName!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextClrUserDefined71(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteClrUserDefinedSqlType(schemaName: sqlTypeContext.InputstringSchemaName!, name: sqlTypeContext.InputstringName!, assemblyName: sqlTypeContext.InputstringAssemblyName!, serializationFormat: sqlTypeContext.InputSqlClrSerializationFormatSerializationFormat!, maxByteSize: sqlTypeContext.InputintMaxByteSize!, isByteOrdered: sqlTypeContext.InputboolIsByteOrdered!, isFixedLength: sqlTypeContext.InputboolIsFixedLength!, validationMethodName: sqlTypeContext.InputstringValidationMethodName!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextClrUserDefined72(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteClrUserDefinedSqlType(schemaName: sqlTypeContext.InputstringSchemaName!, name: sqlTypeContext.InputstringName!, assemblyName: sqlTypeContext.InputstringAssemblyName!, serializationFormat: sqlTypeContext.InputSqlClrSerializationFormatSerializationFormat!, isByteOrdered: sqlTypeContext.InputboolIsByteOrdered!, isFixedLength: sqlTypeContext.InputboolIsFixedLength!, validationMethodName: sqlTypeContext.InputstringValidationMethodName!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextRestore73(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteRestoreSqlType(name: sqlTypeContext.InputSqlTypeNameName!, precision: sqlTypeContext.InputbytePrecision!, scale: sqlTypeContext.InputbyteScale!, length: sqlTypeContext.InputintLength!, isMax: sqlTypeContext.InputboolIsMax!, collation: sqlTypeContext.InputstringCollation!, vectorDimensions: sqlTypeContext.InputushortVectorDimensions!, vectorBaseType: sqlTypeContext.InputSqlVectorBaseTypeVectorBaseType!, xmlContentKind: sqlTypeContext.InputSqlXmlContentKindXmlContentKind!, xmlSchemaCollection: sqlTypeContext.InputSqlXmlSchemaCollectionXmlSchemaCollection!, userTypeKind: sqlTypeContext.InputSqlUserTypeKindUserTypeKind!, userTypeSchema: sqlTypeContext.InputstringUserTypeSchema!, userTypeName: sqlTypeContext.InputstringUserTypeName!, baseType: sqlTypeContext.InputSqlTypeBaseType!, userTypeNullable: sqlTypeContext.InputboolUserTypeNullable!, clrAssemblyName: sqlTypeContext.InputstringClrAssemblyName!, clrClassName: sqlTypeContext.InputstringClrClassName!, clrSerializationFormat: sqlTypeContext.InputSqlClrSerializationFormatClrSerializationFormat!, clrMaxByteSize: sqlTypeContext.InputintClrMaxByteSize!, clrIsByteOrdered: sqlTypeContext.InputboolClrIsByteOrdered!, clrIsFixedLength: sqlTypeContext.InputboolClrIsFixedLength!, clrValidationMethodName: sqlTypeContext.InputstringClrValidationMethodName!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextRebindXmlSchemaCollection74(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSource(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteRebindXmlSchemaCollectionSqlType(sqlType: sqlTypeContext.SqlTypeSource!, collection: sqlTypeContext.InputSqlXmlSchemaCollectionCollection!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextApplyDefaultCollation75(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSource(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteApplyDefaultCollationSqlType(sqlType: sqlTypeContext.SqlTypeSource!, defaultCollation: sqlTypeContext.InputstringDefaultCollation!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextValidate76(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSource(sqlTypeContext: sqlTypeContext);
        ExecuteValidateSqlType(sqlType: sqlTypeContext.SqlTypeSource!, value: sqlTypeContext.InputSqlValueValue!, columnName: sqlTypeContext.InputstringColumnName!);
        return sqlTypeContext;
    });

    public SqlTypeContext ProcessSqlTypeContextNormalizeForStorage77(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSource(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlValueResult = ExecuteNormalizeForStorageSqlType(sqlType: sqlTypeContext.SqlTypeSource!, value: sqlTypeContext.InputSqlValueValue!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextToString80(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContextSource(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            TextResult = ExecuteToStringSqlType(sqlType: sqlTypeContext.SqlTypeSource!)
        };
    });

    private SqlType ExecuteTextWithCollationSqlType(string collation) =>
        ExecuteCharSqlType(name: SqlTypeName.Text, length: null, isMax: true, collation: collation);

    private SqlType ExecuteNTextWithCollationSqlType(string collation) =>
        ExecuteCharSqlType(name: SqlTypeName.NText, length: null, isMax: true, collation: collation);

    private SqlType ExecuteDecimalSqlType(byte precision = 18, byte scale = 0) =>
        ExecuteExactSqlType(name: SqlTypeName.Decimal, precision: precision, scale: scale);

    private SqlType ExecuteNumericSqlType(byte precision = 18, byte scale = 0) =>
        ExecuteExactSqlType(name: SqlTypeName.Numeric, precision: precision, scale: scale);

    private SqlType ExecuteFloatSqlType(byte precision = 53)
    {
        if (precision is < 1 or > 53)
        {
            throw new ArgumentOutOfRangeException(nameof(precision));
        }

        return new SqlType
        {
            Name = SqlTypeName.Float,
            Precision = precision <= 24 ? (byte)24 : (byte)53
        };
    }

    private SqlType ExecuteTimeSqlType(byte scale = 7) =>
        ExecuteScaledTemporalSqlType(name: SqlTypeName.Time, scale: scale);

    private SqlType ExecuteDateTime2SqlType(byte scale = 7) =>
        ExecuteScaledTemporalSqlType(name: SqlTypeName.DateTime2, scale: scale);

    private SqlType ExecuteDateTimeOffsetSqlType(byte scale = 7) =>
        ExecuteScaledTemporalSqlType(name: SqlTypeName.DateTimeOffset, scale: scale);

    private SqlType ExecuteCharSqlType(int length = 1, string? collation = null) =>
        ExecuteCharSqlType(name: SqlTypeName.Char, length: ExecuteValidateLengthSqlType(length: length, maximum: 8_000), isMax: false, collation: collation);

    private SqlType ExecuteVarCharSqlType(int length = 1, string? collation = null) =>
        ExecuteCharSqlType(name: SqlTypeName.VarChar, length: ExecuteValidateLengthSqlType(length: length, maximum: 8_000), isMax: false, collation: collation);

    private SqlType ExecuteVarCharMaxSqlType(string? collation = null) =>
        ExecuteCharSqlType(name: SqlTypeName.VarChar, length: null, isMax: true, collation: collation);

    private SqlType ExecuteNCharSqlType(int length = 1, string? collation = null) =>
        ExecuteCharSqlType(name: SqlTypeName.NChar, length: ExecuteValidateLengthSqlType(length: length, maximum: 4_000), isMax: false, collation: collation);

    private SqlType ExecuteNVarCharSqlType(int length = 1, string? collation = null) =>
        ExecuteCharSqlType(name: SqlTypeName.NVarChar, length: ExecuteValidateLengthSqlType(length: length, maximum: 4_000), isMax: false, collation: collation);

    private SqlType ExecuteNVarCharMaxSqlType(string? collation = null) =>
        ExecuteCharSqlType(name: SqlTypeName.NVarChar, length: null, isMax: true, collation: collation);

    private SqlType ExecuteBinarySqlType(int length = 1) =>
        new SqlType
    {
        Name = SqlTypeName.Binary,
        Length = ExecuteValidateLengthSqlType(length: length, maximum: 8_000)
    };

    private SqlType ExecuteVarBinarySqlType(int length = 1) =>
        new SqlType
    {
        Name = SqlTypeName.VarBinary,
        Length = ExecuteValidateLengthSqlType(length: length, maximum: 8_000)
    };

    private SqlType ExecuteVectorSqlType(ushort dimensions, SqlVectorBaseType baseType = SqlVectorBaseType.Float32)
    {
        if (!Enum.IsDefined(value: baseType))
        {
            throw new ArgumentOutOfRangeException(nameof(baseType));
        }

        if (dimensions is 0 or > 1_998)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions));
        }

        return new SqlType
        {
            Name = SqlTypeName.Vector,
            VectorDimensions = dimensions,
            VectorBaseType = baseType
        };
    }

    private SqlType ExecuteTypedXmlSqlType(SqlXmlSchemaCollection schemaCollection, SqlXmlContentKind contentKind = SqlXmlContentKind.Content)
    {
        ArgumentNullException.ThrowIfNull(argument: schemaCollection);

        if (!Enum.IsDefined(value: contentKind))
        {
            throw new ArgumentOutOfRangeException(nameof(contentKind));
        }

        return new SqlType
        {
            Name = SqlTypeName.Xml,
            IsMax = true,
            XmlContentKind = contentKind,
            XmlSchemaCollection = schemaCollection
        };
    }

    private SqlType ExecuteAliasSqlType(string schemaName, string name, SqlType baseType, bool isNullable = true)
    {
        ArgumentNullException.ThrowIfNull(argument: baseType);

        if (ReadIsUserDefinedSqlType(sqlType: baseType) || baseType.Name is not (SqlTypeName.BigInt or SqlTypeName.Int or SqlTypeName.SmallInt or SqlTypeName.TinyInt or SqlTypeName.Binary or SqlTypeName.VarBinary or SqlTypeName.Bit or SqlTypeName.Char or SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.VarChar or SqlTypeName.Date or SqlTypeName.DateTime or SqlTypeName.DateTime2 or SqlTypeName.DateTimeOffset or SqlTypeName.SmallDateTime or SqlTypeName.Time or SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.Float or SqlTypeName.Real or SqlTypeName.Image or SqlTypeName.Money or SqlTypeName.SmallMoney or SqlTypeName.SqlVariant or SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.UniqueIdentifier))
        {
            throw new ArgumentException("SQL Server does not allow an alias type over this system type.", nameof(baseType));
        }

        schemaName = ValidateIdentifier(value: schemaName, parameterName: nameof(schemaName));
        name = ValidateIdentifier(value: name, parameterName: nameof(name));

        return new SqlType
        {
            Name = baseType.Name,
            Precision = baseType.Precision,
            Scale = baseType.Scale,
            Length = baseType.Length,
            IsMax = baseType.IsMax,
            Collation = baseType.Collation,
            VectorDimensions = baseType.VectorDimensions,
            VectorBaseType = baseType.VectorBaseType,
            XmlContentKind = baseType.XmlContentKind,
            XmlSchemaCollection = baseType.XmlSchemaCollection,
            UserTypeKind = SqlUserTypeKind.Alias,
            UserTypeSchema = schemaName,
            UserTypeName = name,
            BaseType = baseType,
            UserTypeNullable = isNullable
        };
    }

    private SqlType ExecuteClrUserDefinedSqlType(string schemaName, string name, string assemblyName, string className, SqlClrSerializationFormat serializationFormat, int maxByteSize, bool isByteOrdered = false, bool isFixedLength = false, string? validationMethodName = null)
    {
        if (serializationFormat == SqlClrSerializationFormat.Native)
        {
            throw new ArgumentException("FORMAT.Native must not specify MaxByteSize; use the overload without maxByteSize.", nameof(maxByteSize));
        }

        return ExecuteCreateClrUserDefinedSqlType(schemaName: schemaName, name: name, assemblyName: assemblyName, className: className, serializationFormat: serializationFormat, maxByteSize: maxByteSize, isByteOrdered: isByteOrdered, isFixedLength: isFixedLength, validationMethodName: validationMethodName);
    }

    private SqlType ExecuteClrUserDefinedSqlType(string schemaName, string name, string assemblyName, string className, SqlClrSerializationFormat serializationFormat, bool isByteOrdered = false, bool isFixedLength = false, string? validationMethodName = null)
    {
        if (serializationFormat != SqlClrSerializationFormat.Native)
        {
            throw new ArgumentException("FORMAT.UserDefined requires MaxByteSize.", nameof(serializationFormat));
        }

        return ExecuteCreateClrUserDefinedSqlType(schemaName: schemaName, name: name, assemblyName: assemblyName, className: className, serializationFormat: serializationFormat, maxByteSize: null, isByteOrdered: isByteOrdered, isFixedLength: isFixedLength, validationMethodName: validationMethodName);
    }

    private SqlType ExecuteCreateClrUserDefinedSqlType(string schemaName, string name, string assemblyName, string className, SqlClrSerializationFormat serializationFormat, int? maxByteSize, bool isByteOrdered, bool isFixedLength, string? validationMethodName)
    {
        schemaName = ValidateIdentifier(value: schemaName, parameterName: nameof(schemaName));
        name = ValidateIdentifier(value: name, parameterName: nameof(name));
        assemblyName = ValidateIdentifier(value: assemblyName, parameterName: nameof(assemblyName));
        className = ValidateIdentifier(value: className, parameterName: nameof(className));

        if (!Enum.IsDefined(value: serializationFormat))
        {
            throw new ArgumentOutOfRangeException(nameof(serializationFormat));
        }

        if (serializationFormat == SqlClrSerializationFormat.UserDefined && maxByteSize is 0 or < -1 or > 8_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maxByteSize));
        }

        if (serializationFormat == SqlClrSerializationFormat.Native && maxByteSize is not null)
        {
            throw new ArgumentException("FORMAT.Native cannot declare MaxByteSize.", nameof(maxByteSize));
        }

        if (isFixedLength && serializationFormat == SqlClrSerializationFormat.UserDefined && maxByteSize < 1)
        {
            throw new ArgumentException("A fixed-length CLR type requires a finite maximum byte size.", nameof(isFixedLength));
        }

        if (validationMethodName is not null)
        {
            validationMethodName = ValidateIdentifier(value: validationMethodName, parameterName: nameof(validationMethodName));
        }

        return new SqlType
        {
            Name = SqlTypeName.VarBinary,
            Length = maxByteSize is> 0 ? maxByteSize : null,
            IsMax = maxByteSize < 0,
            UserTypeKind = SqlUserTypeKind.Clr,
            UserTypeSchema = schemaName,
            UserTypeName = name,
            ClrAssemblyName = assemblyName,
            ClrClassName = className,
            ClrSerializationFormat = serializationFormat,
            ClrMaxByteSize = maxByteSize,
            ClrIsByteOrdered = isByteOrdered,
            ClrIsFixedLength = isFixedLength,
            ClrValidationMethodName = validationMethodName
        };
    }

    private SqlType ExecuteClrUserDefinedSqlType(string schemaName, string name, string assemblyName, SqlClrSerializationFormat serializationFormat, int maxByteSize, bool isByteOrdered = false, bool isFixedLength = false, string? validationMethodName = null) =>
        ExecuteClrUserDefinedSqlType(schemaName: schemaName, name: name, assemblyName: assemblyName, className: name, serializationFormat: serializationFormat, maxByteSize: maxByteSize, isByteOrdered: isByteOrdered, isFixedLength: isFixedLength, validationMethodName: validationMethodName);

    private SqlType ExecuteClrUserDefinedSqlType(string schemaName, string name, string assemblyName, SqlClrSerializationFormat serializationFormat, bool isByteOrdered = false, bool isFixedLength = false, string? validationMethodName = null) =>
        ExecuteClrUserDefinedSqlType(schemaName: schemaName, name: name, assemblyName: assemblyName, className: name, serializationFormat: serializationFormat, isByteOrdered: isByteOrdered, isFixedLength: isFixedLength, validationMethodName: validationMethodName);

    private SqlType ExecuteRestoreSqlType(SqlTypeName name, byte? precision, byte? scale, int? length, bool isMax, string? collation, ushort? vectorDimensions, SqlVectorBaseType? vectorBaseType, SqlXmlContentKind? xmlContentKind = null, SqlXmlSchemaCollection? xmlSchemaCollection = null, SqlUserTypeKind userTypeKind = SqlUserTypeKind.None, string? userTypeSchema = null, string? userTypeName = null, SqlType? baseType = null, bool? userTypeNullable = null, string? clrAssemblyName = null, string? clrClassName = null, SqlClrSerializationFormat? clrSerializationFormat = null, int? clrMaxByteSize = null, bool clrIsByteOrdered = false, bool clrIsFixedLength = false, string? clrValidationMethodName = null)
    {
        var result = new SqlType
        {
            Name = name,
            Precision = precision,
            Scale = scale,
            Length = length,
            IsMax = isMax,
            Collation = ExecuteNormalizeCollationSqlType(collation: collation),
            VectorDimensions = vectorDimensions,
            VectorBaseType = vectorBaseType,
            XmlContentKind = xmlContentKind,
            XmlSchemaCollection = xmlSchemaCollection,
            UserTypeKind = userTypeKind,
            UserTypeSchema = userTypeSchema,
            UserTypeName = userTypeName,
            BaseType = baseType,
            UserTypeNullable = userTypeNullable,
            ClrAssemblyName = clrAssemblyName,
            ClrClassName = clrClassName,
            ClrSerializationFormat = clrSerializationFormat,
            ClrMaxByteSize = clrMaxByteSize,
            ClrIsByteOrdered = clrIsByteOrdered,
            ClrIsFixedLength = clrIsFixedLength,
            ClrValidationMethodName = clrValidationMethodName
        };

        ExecuteValidateDeclarationSqlType(sqlType: result);
        return result;
    }

    private SqlType ExecuteRebindXmlSchemaCollectionSqlType(SqlType sqlType, SqlXmlSchemaCollection collection)
    {
        ArgumentNullException.ThrowIfNull(argument: collection);

        if (sqlType.Name != SqlTypeName.Xml || sqlType.XmlSchemaCollection is null || sqlType.XmlContentKind is null)
        {
            throw new InvalidOperationException("Only typed XML can be rebound to a schema collection.");
        }

        return ExecuteTypedXmlSqlType(schemaCollection: collection, contentKind: sqlType.XmlContentKind.Value);
    }

    private SqlType ExecuteApplyDefaultCollationSqlType(SqlType sqlType, string defaultCollation)
    {
        _ = collations.ParseSqlCollation(name: defaultCollation);

        if (sqlType.UserTypeKind == SqlUserTypeKind.Alias)
        {
            return ExecuteAliasSqlType(schemaName: sqlType.UserTypeSchema!, name: sqlType.UserTypeName!, baseType: ExecuteApplyDefaultCollationSqlType(sqlType: sqlType.BaseType!, defaultCollation: defaultCollation), isNullable: sqlType.UserTypeNullable!.Value);
        }

        if (sqlType.Collation is not null)
        {
            return sqlType;
        }

        return sqlType.Name switch
        {
            SqlTypeName.Char => ExecuteCharSqlType(length: sqlType.Length!.Value, collation: defaultCollation),
            SqlTypeName.VarChar when sqlType.IsMax => ExecuteVarCharMaxSqlType(collation: defaultCollation),
            SqlTypeName.VarChar => ExecuteVarCharSqlType(length: sqlType.Length!.Value, collation: defaultCollation),
            SqlTypeName.Text => ExecuteTextWithCollationSqlType(collation: defaultCollation),
            SqlTypeName.NChar => ExecuteNCharSqlType(length: sqlType.Length!.Value, collation: defaultCollation),
            SqlTypeName.NVarChar when sqlType.IsMax => ExecuteNVarCharMaxSqlType(collation: defaultCollation),
            SqlTypeName.NVarChar => ExecuteNVarCharSqlType(length: sqlType.Length!.Value, collation: defaultCollation),
            SqlTypeName.NText => ExecuteNTextWithCollationSqlType(collation: defaultCollation),
            _ => sqlType
        };
    }

    private void ExecuteValidateSqlType(SqlType sqlType, SqlValue value, string columnName)
    {
        ArgumentNullException.ThrowIfNull(argument: value);

        if (value.IsNull)
        {
            return;
        }

        if (value.StorageFamily != ReadStorageFamilySqlType(sqlType: sqlType))
        {
            throw new ArgumentException($"Column '{columnName}' expects {ExecuteToStringSqlType(sqlType: sqlType)}, received {value.StorageFamily}.", nameof(value));
        }

        switch (value)
        {
            case IntegerSqlValue integer:
                ExecuteValidateIntegerSqlType(sqlType: sqlType, value: integer.Value, columnName: columnName);
                break;
            case DecimalSqlValue exact:
                ExecuteValidateDecimalSqlType(sqlType: sqlType, value: exact.Value, columnName: columnName);
                break;
            case FloatSqlValue approximate when !double.IsFinite(d: approximate.Value):
                throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' requires a finite SQL floating-point value.");
            case FloatSqlValue approximate when sqlType.Name == SqlTypeName.Real && (approximate.Value < -float.MaxValue || approximate.Value > float.MaxValue):
                throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' exceeds real's range.");
            case TextSqlValue text:
                ExecuteValidateTextSqlType(sqlType: sqlType, value: text.Value, columnName: columnName);
                break;
            case BinarySqlValue binary:
                ExecuteValidateBinarySqlType(sqlType: sqlType, value: ReadBinary(binarySqlValue: binary), columnName: columnName);
                break;
            case HierarchyIdSqlValue hierarchy when sqlType.Name != SqlTypeName.HierarchyId:
                throw new ArgumentException($"Column '{columnName}' is not hierarchyid.", nameof(value));
            case HierarchyIdSqlValue hierarchy:
                break;
            case SpatialSqlValue spatial when sqlType.Name == SqlTypeName.Geometry && spatial.Value.Kind != SqlSpatialKind.Geometry || sqlType.Name == SqlTypeName.Geography && spatial.Value.Kind != SqlSpatialKind.Geography:
                throw new ArgumentException($"Column '{columnName}' has the wrong spatial kind.", nameof(value));
            case SpatialSqlValue when sqlType.Name is not (SqlTypeName.Geometry or SqlTypeName.Geography):
                throw new ArgumentException($"Column '{columnName}' is not spatial.", nameof(value));
            case TimeSqlValue time:
                ExecuteValidateTemporalScaleSqlType(sqlType: sqlType, ticks: time.Value.Ticks, columnName: columnName);
                break;
            case DateTimeSqlValue dateTime:
                ExecuteValidateDateTimeSqlType(sqlType: sqlType, value: dateTime.Value, columnName: columnName);
                break;
            case DateTimeOffsetSqlValue dateTimeOffset:
                ExecuteValidateTemporalScaleSqlType(sqlType: sqlType, ticks: dateTimeOffset.Value.Ticks, columnName: columnName);
                break;
            case VariantSqlValue variant:
                ExecuteValidateVariantSqlType(variant: variant, columnName: columnName);
                break;
            case VectorSqlValue vector:
                ExecuteValidateVectorSqlType(sqlType: sqlType, vector: vector, columnName: columnName);
                break;
        }

        try
        {
            var normalized = ExecuteNormalizeForStorageSqlType(sqlType: sqlType, value: value);

            if (sqlType.Name == SqlTypeName.SmallDateTime && normalized is DateTimeSqlValue small && small.Value >= new DateTime(2079, 6, 7))
            {
                throw new OverflowException();
            }
        }
        catch (Exception exception)when (exception is OverflowException or ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Column '{columnName}' overflows {ExecuteToStringSqlType(sqlType: sqlType)} after SQL assignment rounding: {exception.Message}");
        }
    }

    private SqlValue ExecuteNormalizeForStorageSqlType(SqlType sqlType, SqlValue value)
    {
        if (value.IsNull)
        {
            return value;
        }

        return value switch
        {
            DecimalSqlValue exact when sqlType.Name is SqlTypeName.Money or SqlTypeName.SmallMoney => RoundedDecimal(input: exact.Value, scale: 4),
            DecimalSqlValue exact when sqlType.Name is SqlTypeName.Decimal or SqlTypeName.Numeric => RoundedDecimal(input: exact.Value, scale: sqlType.Scale!.Value),
            FloatSqlValue approximate when sqlType.Name == SqlTypeName.Real || sqlType.Name == SqlTypeName.Float && sqlType.Precision <= 24 => new FloatSqlValue
            {
                Value = (float)approximate.Value == 0f ? 0d : (float)approximate.Value,
                StorageFamily = SqlStorageFamily.Float
            },
            TimeSqlValue time => new TimeSqlValue
            {
                Value = new TimeOnly(ExecuteRoundTicksSqlType(ticks: time.Value.Ticks, scale: sqlType.Scale!.Value) % TimeSpan.TicksPerDay),
                StorageFamily = SqlStorageFamily.Time
            },
            DateTimeSqlValue dateTime => new DateTimeSqlValue
            {
                Value = DateTime.SpecifyKind(value: ExecuteRoundDateTimeValueSqlType(sqlType: sqlType, value: dateTime.Value), kind: DateTimeKind.Unspecified),
                StorageFamily = SqlStorageFamily.DateTime
            },
            DateTimeOffsetSqlValue offset => NormalizeOffset(input: offset.Value),
            TextSqlValue text when sqlType.Name is SqlTypeName.Char or SqlTypeName.NChar => new TextSqlValue
            {
                Value = ExecutePadTextSqlType(sqlType: sqlType, value: text.Value),
                StorageFamily = SqlStorageFamily.Text
            },
            BinarySqlValue binary when sqlType.Name == SqlTypeName.Binary && ReadBinary(binarySqlValue: binary)
            .Length < sqlType.Length => CreateBinarySqlValue(value: ReadBinary(binarySqlValue: binary)
            .Span.ToArray()
            .Concat(second: new byte[sqlType.Length!.Value - ReadBinary(binarySqlValue: binary)
            .Length])
            .ToArray()),
            _ => value
        };

        SqlValue RoundedDecimal(SqlDecimal input, byte scale)
        {
            var coefficient = decimals.RoundSqlDecimal(sqlDecimal: input, scale: scale);

            return new DecimalSqlValue
            {
                Value = decimals.CreateSqlDecimal(coefficient: coefficient, scale: scale),
                StorageFamily = SqlStorageFamily.Decimal
            };
        }

        SqlValue NormalizeOffset(System.DateTimeOffset input)
        {
            var utcTicks = ExecuteRoundTicksSqlType(ticks: input.UtcTicks, scale: sqlType.Scale!.Value);

            return new DateTimeOffsetSqlValue
            {
                Value = new System.DateTimeOffset(checked(utcTicks + input.Offset.Ticks), input.Offset),
                StorageFamily = SqlStorageFamily.DateTimeOffset
            };
        }
    }

    private string ExecutePadTextSqlType(SqlType sqlType, string value)
    {
        var actual = sqlType.Name == SqlTypeName.NChar ? value.Length : collations.CountSqlCollationBytes(sqlCollation: (ReadCollationMetadataSqlType(sqlType: sqlType) ?? collations.ReadDefaultSqlCollation()), value: value);
        return actual >= sqlType.Length ? value : value + new string (' ', sqlType.Length!.Value - actual);
    }

    private DateTime ExecuteRoundDateTimeValueSqlType(SqlType sqlType, DateTime value)
    {
        if (sqlType.Name == SqlTypeName.SmallDateTime)
        {
            var minuteTicks = value.Ticks - value.Ticks % TimeSpan.TicksPerMinute;

            if (value.Ticks - minuteTicks >= TimeSpan.TicksPerSecond * 30)
            {
                minuteTicks += TimeSpan.TicksPerMinute;
            }

            return new DateTime(minuteTicks, DateTimeKind.Unspecified);
        }

        if (sqlType.Name == SqlTypeName.DateTime)
        {
            var units = (long)Math.Round(value: value.TimeOfDay.TotalSeconds * 300d, mode: MidpointRounding.AwayFromZero);
            var ticks = checked(value.Date.Ticks + (long)Math.Round(value: units * (double)TimeSpan.TicksPerSecond / 300d, mode: MidpointRounding.AwayFromZero));
            return new DateTime(ticks, DateTimeKind.Unspecified);
        }

        return new DateTime(ExecuteRoundTicksSqlType(ticks: value.Ticks, scale: sqlType.Scale!.Value), DateTimeKind.Unspecified);
    }

    private long ExecuteRoundTicksSqlType(long ticks, byte scale)
    {
        var quantum = (long)Math.Pow(x: 10, y: 7 - scale);
        var remainder = ticks % quantum;
        return remainder == 0 ? ticks : checked(ticks - remainder + (remainder * 2 >= quantum ? quantum : 0));
    }

    private string ExecuteToStringSqlType(SqlType sqlType)
    {
        if (ReadIsUserDefinedSqlType(sqlType: sqlType))
        {
            return ReadQualifiedUserTypeNameSqlType(sqlType: sqlType)!;
        }

        return sqlType.Name switch
        {
            SqlTypeName.Decimal or SqlTypeName.Numeric => $"{sqlType.Name.ToString()
            .ToLowerInvariant()}({sqlType.Precision},{sqlType.Scale})",
            SqlTypeName.Float => $"float({sqlType.Precision})",
            SqlTypeName.Time or SqlTypeName.DateTime2 or SqlTypeName.DateTimeOffset => $"{sqlType.Name.ToString()
            .ToLowerInvariant()}({sqlType.Scale})",
            SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.Binary or SqlTypeName.VarBinary => $"{sqlType.Name.ToString()
            .ToLowerInvariant()}({(sqlType.IsMax ? "max" : sqlType.Length)})",
            SqlTypeName.Vector => $"vector({sqlType.VectorDimensions},{sqlType.VectorBaseType?.ToString()
            .ToLowerInvariant()})",
            SqlTypeName.Xml when sqlType.XmlSchemaCollection is not null => $"xml({sqlType.XmlContentKind!.Value.ToString()
            .ToLowerInvariant()} {ReadSchemaQualifiedName(sqlXmlSchemaCollection: sqlType.XmlSchemaCollection)})",
            _ => sqlType.Name.ToString()
            .ToLowerInvariant()};
    }

    private SqlType ExecuteExactSqlType(SqlTypeName name, byte precision, byte scale)
    {
        if (precision is < 1 or > 38)
        {
            throw new ArgumentOutOfRangeException(nameof(precision));
        }

        if (scale > precision)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        return new SqlType
        {
            Name = name,
            Precision = precision,
            Scale = scale
        };
    }

    private SqlType ExecuteScaledTemporalSqlType(SqlTypeName name, byte scale)
    {
        if (scale > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        return new SqlType
        {
            Name = name,
            Scale = scale
        };
    }

    private SqlType ExecuteCharSqlType(SqlTypeName name, int? length, bool isMax, string? collation) =>
        new SqlType
    {
        Name = name,
        Length = length,
        IsMax = isMax,
        Collation = ExecuteNormalizeCollationSqlType(collation: collation)
    };

    private int ExecuteValidateLengthSqlType(int length, int maximum)
    {
        if (length < 1 || length > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return length;
    }

    private string? ExecuteNormalizeCollationSqlType(string? collation)
    {
        if (collation is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value: collation))
        {
            throw new ArgumentException("Collation cannot be empty.", nameof(collation));
        }

        if (collation.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(collation), "SQL Server collation names cannot exceed 128 characters.");
        }

        return collation;
    }

    private void ExecuteValidateDeclarationSqlType(SqlType sqlType)
    {
        if (sqlType.UserTypeKind == SqlUserTypeKind.Alias)
        {
            if (sqlType.BaseType is null || ReadIsUserDefinedSqlType(sqlType: sqlType.BaseType) || sqlType.UserTypeNullable is null || string.IsNullOrWhiteSpace(value: sqlType.UserTypeSchema) || string.IsNullOrWhiteSpace(value: sqlType.UserTypeName))
            {
                throw ExecuteInvalidSqlType();
            }

            if (ExecuteAliasSqlType(schemaName: sqlType.UserTypeSchema, name: sqlType.UserTypeName, baseType: sqlType.BaseType, isNullable: sqlType.UserTypeNullable.Value) != sqlType)
            {
                throw ExecuteInvalidSqlType();
            }

            return;
        }

        if (sqlType.UserTypeKind == SqlUserTypeKind.Clr)
        {
            if (sqlType.ClrSerializationFormat is null || string.IsNullOrWhiteSpace(value: sqlType.UserTypeSchema) || string.IsNullOrWhiteSpace(value: sqlType.UserTypeName) || string.IsNullOrWhiteSpace(value: sqlType.ClrAssemblyName) || string.IsNullOrWhiteSpace(value: sqlType.ClrClassName))
            {
                throw ExecuteInvalidSqlType();
            }

            var canonicalClr = sqlType.ClrSerializationFormat == SqlClrSerializationFormat.Native ? ExecuteClrUserDefinedSqlType(schemaName: sqlType.UserTypeSchema, name: sqlType.UserTypeName, assemblyName: sqlType.ClrAssemblyName, className: sqlType.ClrClassName, serializationFormat: sqlType.ClrSerializationFormat.Value, isByteOrdered: sqlType.ClrIsByteOrdered, isFixedLength: sqlType.ClrIsFixedLength, validationMethodName: sqlType.ClrValidationMethodName) : ExecuteClrUserDefinedSqlType(schemaName: sqlType.UserTypeSchema, name: sqlType.UserTypeName, assemblyName: sqlType.ClrAssemblyName, className: sqlType.ClrClassName, serializationFormat: sqlType.ClrSerializationFormat.Value, maxByteSize: sqlType.ClrMaxByteSize ?? throw ExecuteInvalidSqlType(), isByteOrdered: sqlType.ClrIsByteOrdered, isFixedLength: sqlType.ClrIsFixedLength, validationMethodName: sqlType.ClrValidationMethodName);

            if (canonicalClr != sqlType)
            {
                throw ExecuteInvalidSqlType();
            }

            return;
        }

        if (sqlType.UserTypeKind != SqlUserTypeKind.None || sqlType.BaseType is not null || sqlType.UserTypeNullable is not null || sqlType.UserTypeSchema is not null || sqlType.UserTypeName is not null || sqlType.ClrAssemblyName is not null || sqlType.ClrClassName is not null || sqlType.ClrSerializationFormat is not null || sqlType.ClrMaxByteSize is not null || sqlType.ClrIsByteOrdered || sqlType.ClrIsFixedLength || sqlType.ClrValidationMethodName is not null)
        {
            throw ExecuteInvalidSqlType();
        }

        var canonical = sqlType.Name switch
        {
            SqlTypeName.Decimal => ExecuteDecimalSqlType(precision: sqlType.Precision ?? throw ExecuteInvalidSqlType(), scale: sqlType.Scale ?? throw ExecuteInvalidSqlType()),
            SqlTypeName.Numeric => ExecuteNumericSqlType(precision: sqlType.Precision ?? throw ExecuteInvalidSqlType(), scale: sqlType.Scale ?? throw ExecuteInvalidSqlType()),
            SqlTypeName.Float => ExecuteFloatSqlType(precision: sqlType.Precision ?? throw ExecuteInvalidSqlType()),
            SqlTypeName.Time => ExecuteTimeSqlType(scale: sqlType.Scale ?? throw ExecuteInvalidSqlType()),
            SqlTypeName.DateTime2 => ExecuteDateTime2SqlType(scale: sqlType.Scale ?? throw ExecuteInvalidSqlType()),
            SqlTypeName.DateTimeOffset => ExecuteDateTimeOffsetSqlType(scale: sqlType.Scale ?? throw ExecuteInvalidSqlType()),
            SqlTypeName.Char => ExecuteCharSqlType(length: sqlType.Length ?? throw ExecuteInvalidSqlType(), collation: sqlType.Collation),
            SqlTypeName.VarChar when sqlType.IsMax => ExecuteVarCharMaxSqlType(collation: sqlType.Collation),
            SqlTypeName.VarChar => ExecuteVarCharSqlType(length: sqlType.Length ?? throw ExecuteInvalidSqlType(), collation: sqlType.Collation),
            SqlTypeName.NChar => ExecuteNCharSqlType(length: sqlType.Length ?? throw ExecuteInvalidSqlType(), collation: sqlType.Collation),
            SqlTypeName.NVarChar when sqlType.IsMax => ExecuteNVarCharMaxSqlType(collation: sqlType.Collation),
            SqlTypeName.NVarChar => ExecuteNVarCharSqlType(length: sqlType.Length ?? throw ExecuteInvalidSqlType(), collation: sqlType.Collation),
            SqlTypeName.Binary => ExecuteBinarySqlType(length: sqlType.Length ?? throw ExecuteInvalidSqlType()),
            SqlTypeName.VarBinary when sqlType.IsMax => this.sqlTypeValueVarBinaryMax,
            SqlTypeName.VarBinary => ExecuteVarBinarySqlType(length: sqlType.Length ?? throw ExecuteInvalidSqlType()),
            SqlTypeName.Vector => ExecuteVectorSqlType(dimensions: sqlType.VectorDimensions ?? throw ExecuteInvalidSqlType(), baseType: sqlType.VectorBaseType ?? throw ExecuteInvalidSqlType()),
            SqlTypeName.Text => sqlType.Collation is null ? this.sqlTypeValueText : ExecuteTextWithCollationSqlType(collation: sqlType.Collation),
            SqlTypeName.NText => sqlType.Collation is null ? this.sqlTypeValueNText : ExecuteNTextWithCollationSqlType(collation: sqlType.Collation),
            SqlTypeName.Xml when sqlType.XmlSchemaCollection is not null && sqlType.XmlContentKind is not null => ExecuteTypedXmlSqlType(schemaCollection: sqlType.XmlSchemaCollection, contentKind: sqlType.XmlContentKind.Value),
            _ => ExecuteCanonicalSimpleSqlType(name: sqlType.Name)};

        if (canonical != sqlType)
        {
            throw ExecuteInvalidSqlType();
        }
    }

    private SqlType ExecuteCanonicalSimpleSqlType(SqlTypeName name) =>
        name switch
    {
        SqlTypeName.Bit => this.sqlTypeValueBit,
        SqlTypeName.TinyInt => this.sqlTypeValueTinyInt,
        SqlTypeName.SmallInt => this.sqlTypeValueSmallInt,
        SqlTypeName.Int => this.sqlTypeValueInt,
        SqlTypeName.BigInt => this.sqlTypeValueBigInt,
        SqlTypeName.SmallMoney => this.sqlTypeValueSmallMoney,
        SqlTypeName.Money => this.sqlTypeValueMoney,
        SqlTypeName.Real => this.sqlTypeValueReal,
        SqlTypeName.Date => this.sqlTypeValueDate,
        SqlTypeName.SmallDateTime => this.sqlTypeValueSmallDateTime,
        SqlTypeName.DateTime => this.sqlTypeValueDateTime,
        SqlTypeName.UniqueIdentifier => this.sqlTypeValueUniqueIdentifier,
        SqlTypeName.Image => this.sqlTypeValueImage,
        SqlTypeName.RowVersion => this.sqlTypeValueTimestamp,
        SqlTypeName.Timestamp => this.sqlTypeValueTimestamp,
        SqlTypeName.Xml => this.sqlTypeValueXml,
        SqlTypeName.Json => this.sqlTypeValueJson,
        SqlTypeName.SqlVariant => this.sqlTypeValueSqlVariant,
        SqlTypeName.HierarchyId => this.sqlTypeValueHierarchyId,
        SqlTypeName.Geometry => this.sqlTypeValueGeometry,
        SqlTypeName.Geography => this.sqlTypeValueGeography,
        SqlTypeName.Cursor => this.sqlTypeValueCursor,
        SqlTypeName.Table => this.sqlTypeValueTable,
        _ => throw ExecuteInvalidSqlType()};

    private ArgumentException ExecuteInvalidSqlType() =>
        new("Invalid SQL type facet combination.");

    private void ExecuteValidateIntegerSqlType(SqlType sqlType, long value, string columnName)
    {
        var valid = sqlType.Name switch
        {
            SqlTypeName.TinyInt => value is >= byte.MinValue and <= byte.MaxValue,
            SqlTypeName.SmallInt => value is >= short.MinValue and <= short.MaxValue,
            SqlTypeName.Int => value is >= int.MinValue and <= int.MaxValue,
            SqlTypeName.BigInt => true,
            _ => false
        };

        if (!valid)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' exceeds {sqlType.Name}'s range.");
        }
    }

    private void ExecuteValidateDecimalSqlType(SqlType sqlType, SqlDecimal value, string columnName)
    {
        var precision = sqlType.Precision ?? throw new InvalidOperationException();
        var scale = sqlType.Scale ?? throw new InvalidOperationException();

        if (sqlType.Name is SqlTypeName.Money or SqlTypeName.SmallMoney)
        {
            var scaled = decimals.RoundSqlDecimal(sqlDecimal: value, scale: 4);
            var limit = sqlType.Name == SqlTypeName.Money ? new BigInteger(long.MaxValue) : new BigInteger(int.MaxValue);
            var minimum = sqlType.Name == SqlTypeName.Money ? new BigInteger(long.MinValue) : new BigInteger(int.MinValue);

            if (scaled < minimum || scaled > limit)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' exceeds {sqlType.Name}'s range.");
            }

            return;
        }

        var coefficient = decimals.RoundSqlDecimal(sqlDecimal: value, scale: scale);

        if (decimals.CountSqlDecimalDigits(value: BigInteger.Abs(value: coefficient)) > precision)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' does not fit {ExecuteToStringSqlType(sqlType: sqlType)}.");
        }
    }

    private void ExecuteValidateTextSqlType(SqlType sqlType, string value, string columnName)
    {
        if (sqlType.Name == SqlTypeName.Xml)
        {
            var constraint = sqlType.XmlSchemaCollection is null ? "well-formed XML" : $"XML valid for {ReadSchemaQualifiedName(sqlXmlSchemaCollection: sqlType.XmlSchemaCollection)}";

            documents.CheckSqlTypeXmlText(value: value, isDocument: sqlType.XmlContentKind == SqlXmlContentKind.Document, definitions: sqlType.XmlSchemaCollection is null ? null : ReadSchemaDefinitions(sqlXmlSchemaCollection: sqlType.XmlSchemaCollection)
            .ToArray(), constraint: constraint, columnName: columnName);

            return;
        }

        if (sqlType.Name == SqlTypeName.Json)
        {
            var observations = documents.ReadSqlTypeJsonObservations(value: value, columnName: columnName);

            if (observations[0] == 0)
            {
                throw new ArgumentException(message: $"Column '{columnName}' requires a JSON object or array.", paramName: nameof(value));
            }

            ExecuteValidateJsonLimitsSqlType(observations: observations, columnName: columnName);
            return;
        }

        if (sqlType.IsMax || sqlType.Name is SqlTypeName.Text or SqlTypeName.NText)
        {
            return;
        }

        int actual;

        try
        {
            actual = sqlType.Name is SqlTypeName.NChar or SqlTypeName.NVarChar ? value.Length : collations.CountSqlCollationBytes(sqlCollation: (ReadCollationMetadataSqlType(sqlType: sqlType) ?? collations.ReadDefaultSqlCollation()), value: value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException($"Column '{columnName}' contains text that is not representable in " + $"collation '{sqlType.Collation ?? collations.ReadDefaultSqlCollation()
            .Name}'.", nameof(value), exception);
        }

        if (actual > sqlType.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' exceeds {ExecuteToStringSqlType(sqlType: sqlType)}'s length.");
        }
    }

    private void ExecuteValidateJsonLimitsSqlType(int[] observations, string columnName)
    {
        if (observations[1] > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(paramName: "root", message: $"Column '{columnName}' has more than 65,535 properties in one JSON object.");
        }

        if (observations[4] > 7998)
        {
            throw new ArgumentOutOfRangeException(paramName: "root", message: $"Column '{columnName}' contains a JSON key longer than 7,998 UTF-8 bytes.");
        }

        if (observations[3] > 32 * 1024)
        {
            throw new ArgumentOutOfRangeException(paramName: "root", message: $"Column '{columnName}' contains more than 32K unique JSON keys.");
        }

        if (observations[2] > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(paramName: "root", message: $"Column '{columnName}' has more than 65,535 elements in one JSON array.");
        }

        if (observations[5] > 536870911)
        {
            throw new ArgumentOutOfRangeException(paramName: "root", message: $"Column '{columnName}' contains an oversized JSON string value.");
        }
    }

    private void ExecuteValidateBinarySqlType(SqlType sqlType, ReadOnlyMemory<byte> value, string columnName)
    {
        var length = value.Length;

        if (sqlType.UserTypeKind == SqlUserTypeKind.Clr)
        {
            if (sqlType.ClrIsFixedLength && sqlType.ClrMaxByteSize is { } fixedSize && length != fixedSize)
            {
                throw new ArgumentException($"Column '{columnName}' requires exactly {sqlType.ClrMaxByteSize} serialized bytes.", nameof(length));
            }

            if (sqlType.ClrMaxByteSize is >= 0 && length > sqlType.ClrMaxByteSize)
            {
                throw new ArgumentOutOfRangeException(nameof(length), $"Column '{columnName}' exceeds the CLR UDT maximum serialized size.");
            }

            if (sqlType.ClrSerializationFormat == SqlClrSerializationFormat.Native && length > 8_000)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Native CLR UDT serialization cannot exceed 8,000 bytes.");
            }

            return;
        }

        if (sqlType.Name == SqlTypeName.HierarchyId && length > 892)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"Column '{columnName}' exceeds hierarchyid's 892-byte representation limit.");
        }

        if (sqlType.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp && length != 8)
        {
            throw new ArgumentException($"Column '{columnName}' requires exactly 8 bytes.", nameof(length));
        }

        if (!sqlType.IsMax && sqlType.Length is { } maximum && length > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"Column '{columnName}' exceeds {ExecuteToStringSqlType(sqlType: sqlType)}'s length.");
        }
    }

    private void ExecuteValidateTemporalScaleSqlType(SqlType sqlType, long ticks, string columnName)
    {
        _ = sqlType.Scale ?? throw new InvalidOperationException();
    }

    private void ExecuteValidateDateTimeSqlType(SqlType sqlType, DateTime value, string columnName)
    {
        switch (sqlType.Name)
        {
            case SqlTypeName.SmallDateTime:
                if (value < new DateTime(1900, 1, 1) || value >= new DateTime(2079, 6, 7))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' is not a valid smalldatetime.");
                }

                break;
            case SqlTypeName.DateTime:
                if (value < new DateTime(1753, 1, 1))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' is not a valid datetime.");
                }

                break;
            case SqlTypeName.DateTime2:
                ExecuteValidateTemporalScaleSqlType(sqlType: sqlType, ticks: value.Ticks, columnName: columnName);
                break;
        }
    }

    private void ExecuteValidateVariantSqlType(VariantSqlValue variant, string columnName)
    {
        if (!ReadCanBeSqlVariantValueSqlType(sqlType: variant.DeclaredType) || variant.Value switch
        {
            TextSqlValue text => (variant.DeclaredType.Name is SqlTypeName.NChar or SqlTypeName.NVarChar ? checked(text.Value.Length * 2) : collations.CountSqlCollationBytes(sqlCollation: (ReadCollationMetadataSqlType(sqlType: variant.DeclaredType) ?? collations.ReadDefaultSqlCollation()), value: text.Value)) > 8_000,
            BinarySqlValue binary => ReadBinary(binarySqlValue: binary)
            .Length > 8_000,
            _ => false
        })
        {
            throw new ArgumentException($"Column '{columnName}' contains a value which SQL Server sql_variant cannot store.");
        }
    }

    private void ExecuteValidateVectorSqlType(SqlType sqlType, VectorSqlValue vector, string columnName)
    {
        if (vector.BaseType != sqlType.VectorBaseType || ReadVectorCount(vectorSqlValue: vector) != sqlType.VectorDimensions)
        {
            throw new ArgumentException($"Column '{columnName}' expects {ExecuteToStringSqlType(sqlType: sqlType)}.", nameof(vector));
        }
    }

    private sql_storage_engine.Models.Rows.SqlCollation? ReadCollationMetadataSqlType(SqlType sqlType) =>
        sqlType.Collation is null ? null : collations.ParseSqlCollation(name: sqlType.Collation);

    private bool ReadIsUserDefinedSqlType(SqlType sqlType) =>
        sqlType.UserTypeKind != SqlUserTypeKind.None;

    private string? ReadQualifiedUserTypeNameSqlType(SqlType sqlType) =>
        ReadIsUserDefinedSqlType(sqlType: sqlType) ? $"[{sqlType.UserTypeSchema}].[{sqlType.UserTypeName}]" : null;

    private SqlStorageFamily ReadStorageFamilySqlType(SqlType sqlType) =>
        sqlType.Name switch
    {
        SqlTypeName.Bit => SqlStorageFamily.Boolean,
        SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or SqlTypeName.BigInt => SqlStorageFamily.Integer,
        SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.SmallMoney or SqlTypeName.Money => SqlStorageFamily.Decimal,
        SqlTypeName.Real or SqlTypeName.Float => SqlStorageFamily.Float,
        SqlTypeName.Date => SqlStorageFamily.Date,
        SqlTypeName.Time => SqlStorageFamily.Time,
        SqlTypeName.SmallDateTime or SqlTypeName.DateTime or SqlTypeName.DateTime2 => SqlStorageFamily.DateTime,
        SqlTypeName.DateTimeOffset => SqlStorageFamily.DateTimeOffset,
        SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.Text or SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText or SqlTypeName.Xml or SqlTypeName.Json => SqlStorageFamily.Text,
        SqlTypeName.Binary or SqlTypeName.VarBinary or SqlTypeName.Image or SqlTypeName.RowVersion or SqlTypeName.Timestamp or SqlTypeName.HierarchyId or SqlTypeName.Geometry or SqlTypeName.Geography => SqlStorageFamily.Binary,
        SqlTypeName.UniqueIdentifier => SqlStorageFamily.UniqueIdentifier,
        SqlTypeName.SqlVariant => SqlStorageFamily.Variant,
        SqlTypeName.Vector => SqlStorageFamily.Vector,
        _ => throw new InvalidOperationException($"{sqlType.Name} is not a storable column type.")};

    private bool ReadCanBeColumnSqlType(SqlType sqlType) =>
        sqlType.Name is not (SqlTypeName.Cursor or SqlTypeName.Table);

    private bool ReadCanBeBTreeKeySqlType(SqlType sqlType) =>
        sqlType.UserTypeKind == SqlUserTypeKind.Clr ? sqlType.ClrIsByteOrdered : sqlType.Name switch
    {
        SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image or SqlTypeName.Xml or SqlTypeName.Json or SqlTypeName.Geometry or SqlTypeName.Geography or SqlTypeName.Vector => false,
        SqlTypeName.VarChar or SqlTypeName.NVarChar or SqlTypeName.VarBinary when sqlType.IsMax => false,
        SqlTypeName.Cursor or SqlTypeName.Table => false,
        _ => true
    };

    private bool ReadCanBeSqlVariantValueSqlType(SqlType sqlType) =>
        !ReadIsUserDefinedSqlType(sqlType: sqlType) && ReadCanBeColumnSqlType(sqlType: sqlType) && sqlType.Name switch
    {
        SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image or SqlTypeName.Xml or SqlTypeName.Json or SqlTypeName.SqlVariant or SqlTypeName.HierarchyId or SqlTypeName.Geometry or SqlTypeName.Geography or SqlTypeName.Vector or SqlTypeName.RowVersion or SqlTypeName.Timestamp => false,
        SqlTypeName.VarChar or SqlTypeName.NVarChar or SqlTypeName.VarBinary when sqlType.IsMax => false,
        _ => true
    };
    private readonly SqlType sqlTypeValueBit;
    private readonly SqlType sqlTypeValueTinyInt;
    private readonly SqlType sqlTypeValueSmallInt;
    private readonly SqlType sqlTypeValueInt;
    private readonly SqlType sqlTypeValueBigInt;
    private readonly SqlType sqlTypeValueSmallMoney;
    private readonly SqlType sqlTypeValueMoney;
    private readonly SqlType sqlTypeValueReal;
    private readonly SqlType sqlTypeValueDate;
    private readonly SqlType sqlTypeValueSmallDateTime;
    private readonly SqlType sqlTypeValueDateTime;
    private readonly SqlType sqlTypeValueUniqueIdentifier;
    private readonly SqlType sqlTypeValueText;
    private readonly SqlType sqlTypeValueNText;
    private readonly SqlType sqlTypeValueImage;
    private readonly SqlType sqlTypeValueTimestamp;
    private readonly SqlType sqlTypeValueXml;
    private readonly SqlType sqlTypeValueJson;
    private readonly SqlType sqlTypeValueSqlVariant;
    private readonly SqlType sqlTypeValueHierarchyId;
    private readonly SqlType sqlTypeValueGeometry;
    private readonly SqlType sqlTypeValueGeography;
    private readonly SqlType sqlTypeValueCursor;
    private readonly SqlType sqlTypeValueTable;
    private readonly SqlType sqlTypeValueVarBinaryMax;

    private static string ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value: value))
        {
            throw new ArgumentException("SQL identifier cannot be empty.", parameterName);
        }

        if (value.Length > 128)
        {
            throw new ArgumentOutOfRangeException(parameterName, "SQL identifiers cannot exceed 128 characters.");
        }

        return value;
    }

    public SqlTypeContext CreateSqlTypeContextCatalogScalarType(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateCatalogScalarTypeCreation(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            CatalogScalarTypeResult = new sql_storage_engine.Models.Catalog.CatalogScalarType
            {
                Definition = sqlTypeContext.SqlTypeSource!
            }
        };
    });

    private static IReadOnlyList<string> ReadSchemaDefinitions(SqlXmlSchemaCollection sqlXmlSchemaCollection)
    {
        var content = sqlXmlSchemaCollection.DefinitionContent;
        var definitions = new List<string>();
        var offset = 0;

        while (offset < content.Length)
        {
            var colon = content.IndexOf(value: ':', startIndex: offset);

            if (colon < 0 || !int.TryParse(s: content.AsSpan(start: offset, length: colon - offset), style: NumberStyles.None, provider: CultureInfo.InvariantCulture, result: out var length) || length <= 0 || length > content.Length - colon - 1)
            {
                throw new StorageFormatException(message: "XML schema definition content is invalid.");
            }

            definitions.Add(item: content.Substring(startIndex: colon + 1, length: length));
            offset = colon + 1 + length;
        }

        if (definitions.Count == 0)
        {
            throw new StorageFormatException(message: "XML schema definition content is empty.");
        }

        return definitions.AsReadOnly();
    }

    private static string ReadSchemaQualifiedName(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        $"[{sqlXmlSchemaCollection.SchemaName}].[{sqlXmlSchemaCollection.Name}]";

    private static ReadOnlyMemory<byte> ReadBinary(BinarySqlValue binarySqlValue) =>
        binarySqlValue.Content.Select(selector: item => checked((byte)item))
            .ToArray();

    private static BinarySqlValue CreateBinarySqlValue(ReadOnlySpan<byte> value) =>
        new BinarySqlValue
    {
        Content = sql_storage_engine.Brokers.BinaryPayloads.BinaryPayloadBroker.ReadBinaryContent(bytes: value),
        StorageFamily = SqlStorageFamily.Binary
    };

    private static int ReadVectorCount(VectorSqlValue vectorSqlValue) =>
        vectorSqlValue.Content.Length / (vectorSqlValue.BaseType == SqlVectorBaseType.Float16 ? 1 : 2);

    public SqlTypeContext ProcessSqlTypeContextDecimalParse(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            DecimalResult = decimals.ParseSqlDecimal(value: sqlTypeContext.TextResult!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextDecimalCreate(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            DecimalResult = decimals.CreateSqlDecimal(coefficient: sqlTypeContext.CoefficientSource, scale: sqlTypeContext.DecimalScale)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextDecimalFormat(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            TextResult = decimals.FormatSqlDecimal(sqlDecimal: sqlTypeContext.DecimalSource!.Value)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextDecimalRound(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            CoefficientResult = decimals.RoundSqlDecimal(sqlDecimal: sqlTypeContext.DecimalSource!.Value, scale: sqlTypeContext.DecimalScale)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextVectorParse(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            VectorResult = documents.ReadSqlTypeJsonVector(value: sqlTypeContext.TextResult!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextVectorFormat(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            TextResult = documents.WriteSqlTypeJsonVector(values: sqlTypeContext.VectorSource!)
        };
    });

    public SqlTypeContext ProcessSqlTypeContextUtf8(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            BinaryResult = documents.ReadSqlTypeUtf8Bytes(value: sqlTypeContext.TextResult!)
        };
    });

    internal static SqlTypeContextOrchestrationService CreateSqlTypeContextOrchestrationService() =>
        new SqlTypeContextOrchestrationService(sqlCollationService: new sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService(), sqlTypeDocumentService: new sql_storage_engine.Services.Foundations.SqlTypeDocuments.SqlTypeDocumentService(), sqlDecimalService: new sql_storage_engine.Services.Foundations.SqlDecimals.SqlDecimalService());
}