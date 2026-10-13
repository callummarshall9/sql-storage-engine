// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal sealed partial class SqlCollationCatalogSqlTypeProcessingService : ISqlCollationCatalogSqlTypeProcessingService
{
    private readonly sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService collations;
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
    public SqlCollationCatalogSqlTypeProcessingService(sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService sqlCollationService)
    {
        collations = sqlCollationService;
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
        sqlTypeValueText = ExecuteCharacterSqlType(name: SqlTypeName.Text, length: null, isMax: true, collation: null);
        sqlTypeValueNText = ExecuteCharacterSqlType(name: SqlTypeName.NText, length: null, isMax: true, collation: null);
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

    public SqlTypeContext ProcessSqlTypeContextRestore73(SqlTypeContext sqlTypeContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlTypeContext(sqlTypeContext: sqlTypeContext);

        return sqlTypeContext with
        {
            SqlTypeResult = ExecuteRestoreSqlType(name: sqlTypeContext.InputSqlTypeNameName!, precision: sqlTypeContext.InputbytePrecision!, scale: sqlTypeContext.InputbyteScale!, length: sqlTypeContext.InputintLength!, isMax: sqlTypeContext.InputboolIsMax!, collation: sqlTypeContext.InputstringCollation!, vectorDimensions: sqlTypeContext.InputushortVectorDimensions!, vectorBaseType: sqlTypeContext.InputSqlVectorBaseTypeVectorBaseType!, xmlContentKind: sqlTypeContext.InputSqlXmlContentKindXmlContentKind!, xmlSchemaCollection: sqlTypeContext.InputSqlXmlSchemaCollectionXmlSchemaCollection!, userTypeKind: sqlTypeContext.InputSqlUserTypeKindUserTypeKind!, userTypeSchema: sqlTypeContext.InputstringUserTypeSchema!, userTypeName: sqlTypeContext.InputstringUserTypeName!, baseType: sqlTypeContext.InputSqlTypeBaseType!, userTypeNullable: sqlTypeContext.InputboolUserTypeNullable!, clrAssemblyName: sqlTypeContext.InputstringClrAssemblyName!, clrClassName: sqlTypeContext.InputstringClrClassName!, clrSerializationFormat: sqlTypeContext.InputSqlClrSerializationFormatClrSerializationFormat!, clrMaxByteSize: sqlTypeContext.InputintClrMaxByteSize!, clrIsByteOrdered: sqlTypeContext.InputboolClrIsByteOrdered!, clrIsFixedLength: sqlTypeContext.InputboolClrIsFixedLength!, clrValidationMethodName: sqlTypeContext.InputstringClrValidationMethodName!)
        };
    });

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
            SqlTypeName.VarBinary when sqlType.IsMax => ReadVarBinaryMaxSqlType(),
            SqlTypeName.VarBinary => ExecuteVarBinarySqlType(length: sqlType.Length ?? throw ExecuteInvalidSqlType()),
            SqlTypeName.Vector => ExecuteVectorSqlType(dimensions: sqlType.VectorDimensions ?? throw ExecuteInvalidSqlType(), baseType: sqlType.VectorBaseType ?? throw ExecuteInvalidSqlType()),
            SqlTypeName.Text => sqlType.Collation is null ? ReadTextSqlType() : ExecuteTextWithCollationSqlType(collation: sqlType.Collation),
            SqlTypeName.NText => sqlType.Collation is null ? ReadNTextSqlType() : ExecuteNTextWithCollationSqlType(collation: sqlType.Collation),
            SqlTypeName.Xml when sqlType.XmlSchemaCollection is not null && sqlType.XmlContentKind is not null => ExecuteTypedXmlSqlType(schemaCollection: sqlType.XmlSchemaCollection, contentKind: sqlType.XmlContentKind.Value),
            _ => ExecuteCanonicalSimpleSqlType(name: sqlType.Name)};

        if (canonical != sqlType)
        {
            throw ExecuteInvalidSqlType();
        }
    }

    private bool ReadIsUserDefinedSqlType(SqlType sqlType) =>
        sqlType.UserTypeKind != SqlUserTypeKind.None;

    private ArgumentException ExecuteInvalidSqlType() =>
        new("Invalid SQL type facet combination.");

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
        ExecuteCharacterSqlType(name: SqlTypeName.Char, length: ExecuteValidateLengthSqlType(length: length, maximum: 8_000), isMax: false, collation: collation);

    private SqlType ExecuteVarCharMaxSqlType(string? collation = null) =>
        ExecuteCharacterSqlType(name: SqlTypeName.VarChar, length: null, isMax: true, collation: collation);

    private SqlType ExecuteVarCharSqlType(int length = 1, string? collation = null) =>
        ExecuteCharacterSqlType(name: SqlTypeName.VarChar, length: ExecuteValidateLengthSqlType(length: length, maximum: 8_000), isMax: false, collation: collation);

    private SqlType ExecuteNCharSqlType(int length = 1, string? collation = null) =>
        ExecuteCharacterSqlType(name: SqlTypeName.NChar, length: ExecuteValidateLengthSqlType(length: length, maximum: 4_000), isMax: false, collation: collation);

    private SqlType ExecuteNVarCharMaxSqlType(string? collation = null) =>
        ExecuteCharacterSqlType(name: SqlTypeName.NVarChar, length: null, isMax: true, collation: collation);

    private SqlType ExecuteNVarCharSqlType(int length = 1, string? collation = null) =>
        ExecuteCharacterSqlType(name: SqlTypeName.NVarChar, length: ExecuteValidateLengthSqlType(length: length, maximum: 4_000), isMax: false, collation: collation);

    private SqlType ExecuteBinarySqlType(int length = 1) =>
        new SqlType
    {
        Name = SqlTypeName.Binary,
        Length = ExecuteValidateLengthSqlType(length: length, maximum: 8_000)
    };

    private SqlType ReadVarBinaryMaxSqlType() =>
        sqlTypeValueVarBinaryMax;

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

    private SqlType ReadTextSqlType() =>
        sqlTypeValueText;

    private SqlType ExecuteTextWithCollationSqlType(string collation) =>
        ExecuteCharacterSqlType(name: SqlTypeName.Text, length: null, isMax: true, collation: collation);

    private SqlType ReadNTextSqlType() =>
        sqlTypeValueNText;

    private SqlType ExecuteNTextWithCollationSqlType(string collation) =>
        ExecuteCharacterSqlType(name: SqlTypeName.NText, length: null, isMax: true, collation: collation);

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

    private SqlType ExecuteCanonicalSimpleSqlType(SqlTypeName name) =>
        name switch
    {
        SqlTypeName.Bit => ReadBitSqlType(),
        SqlTypeName.TinyInt => ReadTinyIntSqlType(),
        SqlTypeName.SmallInt => ReadSmallIntSqlType(),
        SqlTypeName.Int => ReadIntSqlType(),
        SqlTypeName.BigInt => ReadBigIntSqlType(),
        SqlTypeName.SmallMoney => ReadSmallMoneySqlType(),
        SqlTypeName.Money => ReadMoneySqlType(),
        SqlTypeName.Real => ReadRealSqlType(),
        SqlTypeName.Date => ReadDateSqlType(),
        SqlTypeName.SmallDateTime => ReadSmallDateTimeSqlType(),
        SqlTypeName.DateTime => ReadDateTimeSqlType(),
        SqlTypeName.UniqueIdentifier => ReadUniqueIdentifierSqlType(),
        SqlTypeName.Image => ReadImageSqlType(),
        SqlTypeName.RowVersion => ReadTimestampSqlType(),
        SqlTypeName.Timestamp => ReadTimestampSqlType(),
        SqlTypeName.Xml => ReadXmlSqlType(),
        SqlTypeName.Json => ReadJsonSqlType(),
        SqlTypeName.SqlVariant => ReadSqlVariantSqlType(),
        SqlTypeName.HierarchyId => ReadHierarchyIdSqlType(),
        SqlTypeName.Geometry => ReadGeometrySqlType(),
        SqlTypeName.Geography => ReadGeographySqlType(),
        SqlTypeName.Cursor => ReadCursorSqlType(),
        SqlTypeName.Table => ReadTableSqlType(),
        _ => throw ExecuteInvalidSqlType()};

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

    private SqlType ExecuteCharacterSqlType(SqlTypeName name, int? length, bool isMax, string? collation) =>
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

    private SqlType ReadBitSqlType() =>
        sqlTypeValueBit;

    private SqlType ReadTinyIntSqlType() =>
        sqlTypeValueTinyInt;

    private SqlType ReadSmallIntSqlType() =>
        sqlTypeValueSmallInt;

    private SqlType ReadIntSqlType() =>
        sqlTypeValueInt;

    private SqlType ReadBigIntSqlType() =>
        sqlTypeValueBigInt;

    private SqlType ReadSmallMoneySqlType() =>
        sqlTypeValueSmallMoney;

    private SqlType ReadMoneySqlType() =>
        sqlTypeValueMoney;

    private SqlType ReadRealSqlType() =>
        sqlTypeValueReal;

    private SqlType ReadDateSqlType() =>
        sqlTypeValueDate;

    private SqlType ReadSmallDateTimeSqlType() =>
        sqlTypeValueSmallDateTime;

    private SqlType ReadDateTimeSqlType() =>
        sqlTypeValueDateTime;

    private SqlType ReadUniqueIdentifierSqlType() =>
        sqlTypeValueUniqueIdentifier;

    private SqlType ReadImageSqlType() =>
        sqlTypeValueImage;

    private SqlType ReadTimestampSqlType() =>
        sqlTypeValueTimestamp;

    private SqlType ReadXmlSqlType() =>
        sqlTypeValueXml;

    private SqlType ReadJsonSqlType() =>
        sqlTypeValueJson;

    private SqlType ReadSqlVariantSqlType() =>
        sqlTypeValueSqlVariant;

    private SqlType ReadHierarchyIdSqlType() =>
        sqlTypeValueHierarchyId;

    private SqlType ReadGeometrySqlType() =>
        sqlTypeValueGeometry;

    private SqlType ReadGeographySqlType() =>
        sqlTypeValueGeography;

    private SqlType ReadCursorSqlType() =>
        sqlTypeValueCursor;

    private SqlType ReadTableSqlType() =>
        sqlTypeValueTable;

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

    internal static SqlCollationCatalogSqlTypeProcessingService CreateSqlCollationCatalogSqlTypeProcessingService() =>
        new SqlCollationCatalogSqlTypeProcessingService(sqlCollationService: new sql_storage_engine.Services.Foundations.SqlCollations.SqlCollationService());
}