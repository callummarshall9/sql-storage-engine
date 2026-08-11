using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Schema;

namespace sql_storage_engine.Rows;

/// <summary>The names of the system data types exposed by SQL Server 2025.</summary>
public enum SqlTypeName : byte
{
    Bit = 1,
    TinyInt,
    SmallInt,
    Int,
    BigInt,
    Decimal,
    Numeric,
    SmallMoney,
    Money,
    Real,
    Float,
    Date,
    Time,
    SmallDateTime,
    DateTime,
    DateTime2,
    DateTimeOffset,
    Char,
    VarChar,
    Text,
    NChar,
    NVarChar,
    NText,
    Binary,
    VarBinary,
    Image,
    RowVersion,
    Timestamp,
    UniqueIdentifier,
    Xml,
    Json,
    SqlVariant,
    HierarchyId,
    Geometry,
    Geography,
    Vector,
    Cursor,
    Table
}

/// <summary>The physical value family used by row and index codecs.</summary>
public enum SqlStorageFamily : byte
{
    Boolean = 1,
    Integer,
    Decimal,
    Float,
    Date,
    Time,
    DateTime,
    DateTimeOffset,
    Text,
    Binary,
    UniqueIdentifier,
    Variant,
    Vector
}

public enum SqlVectorBaseType : byte
{
    Float32 = 0,
    Float16 = 1
}

/// <summary>The XML instance constraint attached to a typed XML declaration.</summary>
public enum SqlXmlContentKind : byte
{
    Content = 1,
    Document = 2
}

/// <summary>The kind of database-scoped user-defined scalar type represented by a declaration.</summary>
public enum SqlUserTypeKind : byte
{
    None = 0,
    Alias = 1,
    Clr = 2
}

/// <summary>The serialization contract declared by a SQL CLR user-defined type.</summary>
public enum SqlClrSerializationFormat : byte
{
    Native = 1,
    UserDefined = 2
}

/// <summary>A persisted XML schema collection used by a typed XML declaration.</summary>
public sealed class SqlXmlSchemaCollection : IEquatable<SqlXmlSchemaCollection>
{
    public SqlXmlSchemaCollection(string schemaName, string name, string definition)
        : this(schemaName, name, [definition])
    {
    }

    public SqlXmlSchemaCollection(string schemaName, string name, IEnumerable<string> definitions)
    {
        SchemaName = ValidateIdentifier(schemaName, nameof(schemaName));
        Name = ValidateIdentifier(name, nameof(name));
        ArgumentNullException.ThrowIfNull(definitions);
        _definitions = definitions.ToArray();
        if (_definitions.Length == 0 || _definitions.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("XML schema collection definitions cannot be empty.", nameof(definitions));
        // Compile at declaration time so invalid catalog metadata cannot be created.
        _ = CreateSchemaSet();
    }

    private readonly string[] _definitions;

    public string SchemaName { get; }
    public string Name { get; }
    public string Definition => _definitions[0];
    public IReadOnlyList<string> Definitions => Array.AsReadOnly(_definitions);
    public string QualifiedName => $"[{SchemaName}].[{Name}]";

    internal XmlSchemaSet CreateSchemaSet()
    {
        var schemas = new XmlSchemaSet();
        foreach (var definition in _definitions)
        {
            using var reader = XmlReader.Create(new StringReader(definition), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                ConformanceLevel = ConformanceLevel.Document
            });
            schemas.Add(null, reader);
        }
        schemas.Compile();
        return schemas;
    }

    public bool Equals(SqlXmlSchemaCollection? other) => other is not null &&
        StringComparer.Ordinal.Equals(SchemaName, other.SchemaName) &&
        StringComparer.Ordinal.Equals(Name, other.Name) &&
        _definitions.SequenceEqual(other._definitions, StringComparer.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as SqlXmlSchemaCollection);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SchemaName, StringComparer.Ordinal);
        hash.Add(Name, StringComparer.Ordinal);
        foreach (var definition in _definitions) hash.Add(definition, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    internal static string ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("SQL identifier cannot be empty.", parameterName);
        if (value.Length > 128) throw new ArgumentOutOfRangeException(parameterName, "SQL identifiers cannot exceed 128 characters.");
        return value;
    }
}

/// <summary>
/// A complete SQL Server system-type declaration. Facets which do not apply to a type are null.
/// Length is measured in bytes for non-Unicode strings/binary and UTF-16 code units for Unicode strings.
/// </summary>
public sealed record SqlType
{
    private SqlType(SqlTypeName name, byte? precision = null, byte? scale = null, int? length = null,
        bool isMax = false, string? collation = null, ushort? vectorDimensions = null,
        SqlVectorBaseType? vectorBaseType = null, SqlXmlContentKind? xmlContentKind = null,
        SqlXmlSchemaCollection? xmlSchemaCollection = null, SqlUserTypeKind userTypeKind = SqlUserTypeKind.None,
        string? userTypeSchema = null, string? userTypeName = null, SqlType? baseType = null,
        bool? userTypeNullable = null, string? clrAssemblyName = null, string? clrClassName = null,
        SqlClrSerializationFormat? clrSerializationFormat = null, int? clrMaxByteSize = null,
        bool clrIsByteOrdered = false, bool clrIsFixedLength = false, string? clrValidationMethodName = null)
    {
        Name = name;
        Precision = precision;
        Scale = scale;
        Length = length;
        IsMax = isMax;
        Collation = collation;
        VectorDimensions = vectorDimensions;
        VectorBaseType = vectorBaseType;
        XmlContentKind = xmlContentKind;
        XmlSchemaCollection = xmlSchemaCollection;
        UserTypeKind = userTypeKind;
        UserTypeSchema = userTypeSchema;
        UserTypeName = userTypeName;
        BaseType = baseType;
        UserTypeNullable = userTypeNullable;
        ClrAssemblyName = clrAssemblyName;
        ClrClassName = clrClassName;
        ClrSerializationFormat = clrSerializationFormat;
        ClrMaxByteSize = clrMaxByteSize;
        ClrIsByteOrdered = clrIsByteOrdered;
        ClrIsFixedLength = clrIsFixedLength;
        ClrValidationMethodName = clrValidationMethodName;
    }

    public SqlTypeName Name { get; }
    public byte? Precision { get; }
    public byte? Scale { get; }
    public int? Length { get; }
    public bool IsMax { get; }
    public string? Collation { get; }
    public SqlCollation? CollationMetadata => Collation is null ? null : SqlCollation.Parse(Collation);
    public ushort? VectorDimensions { get; }
    public SqlVectorBaseType? VectorBaseType { get; }
    public SqlXmlContentKind? XmlContentKind { get; }
    public SqlXmlSchemaCollection? XmlSchemaCollection { get; }
    public SqlUserTypeKind UserTypeKind { get; }
    public string? UserTypeSchema { get; }
    public string? UserTypeName { get; }
    public SqlType? BaseType { get; }
    public bool? UserTypeNullable { get; }
    public string? ClrAssemblyName { get; }
    public string? ClrClassName { get; }
    public SqlClrSerializationFormat? ClrSerializationFormat { get; }
    public int? ClrMaxByteSize { get; }
    public bool ClrIsByteOrdered { get; }
    public bool ClrIsFixedLength { get; }
    public string? ClrValidationMethodName { get; }
    public bool IsUserDefined => UserTypeKind != SqlUserTypeKind.None;
    public string? QualifiedUserTypeName => IsUserDefined ? $"[{UserTypeSchema}].[{UserTypeName}]" : null;

    public SqlStorageFamily StorageFamily => Name switch
    {
        SqlTypeName.Bit => SqlStorageFamily.Boolean,
        SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or SqlTypeName.BigInt => SqlStorageFamily.Integer,
        SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.SmallMoney or SqlTypeName.Money => SqlStorageFamily.Decimal,
        SqlTypeName.Real or SqlTypeName.Float => SqlStorageFamily.Float,
        SqlTypeName.Date => SqlStorageFamily.Date,
        SqlTypeName.Time => SqlStorageFamily.Time,
        SqlTypeName.SmallDateTime or SqlTypeName.DateTime or SqlTypeName.DateTime2 => SqlStorageFamily.DateTime,
        SqlTypeName.DateTimeOffset => SqlStorageFamily.DateTimeOffset,
        SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.Text or SqlTypeName.NChar or SqlTypeName.NVarChar or
            SqlTypeName.NText or SqlTypeName.Xml or SqlTypeName.Json => SqlStorageFamily.Text,
        SqlTypeName.Binary or SqlTypeName.VarBinary or SqlTypeName.Image or SqlTypeName.RowVersion or SqlTypeName.Timestamp or
            SqlTypeName.HierarchyId or SqlTypeName.Geometry or SqlTypeName.Geography => SqlStorageFamily.Binary,
        SqlTypeName.UniqueIdentifier => SqlStorageFamily.UniqueIdentifier,
        SqlTypeName.SqlVariant => SqlStorageFamily.Variant,
        SqlTypeName.Vector => SqlStorageFamily.Vector,
        _ => throw new InvalidOperationException($"{Name} is not a storable column type.")
    };

    public bool CanBeColumn => Name is not (SqlTypeName.Cursor or SqlTypeName.Table);

    public bool CanBeBTreeKey => UserTypeKind == SqlUserTypeKind.Clr ? ClrIsByteOrdered : Name switch
    {
        SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image or SqlTypeName.Xml or SqlTypeName.Json or
            SqlTypeName.Geometry or SqlTypeName.Geography or SqlTypeName.Vector => false,
        SqlTypeName.VarChar or SqlTypeName.NVarChar or SqlTypeName.VarBinary when IsMax => false,
        SqlTypeName.Cursor or SqlTypeName.Table => false,
        _ => true
    };

    public bool CanBeSqlVariantValue => !IsUserDefined && CanBeColumn && Name switch
    {
        SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image or SqlTypeName.Xml or SqlTypeName.Json or
            SqlTypeName.SqlVariant or SqlTypeName.HierarchyId or SqlTypeName.Geometry or SqlTypeName.Geography or
            SqlTypeName.Vector or SqlTypeName.RowVersion or SqlTypeName.Timestamp => false,
        SqlTypeName.VarChar or SqlTypeName.NVarChar or SqlTypeName.VarBinary when IsMax => false,
        _ => true
    };

    public static SqlType Bit { get; } = new(SqlTypeName.Bit);
    public static SqlType TinyInt { get; } = new(SqlTypeName.TinyInt);
    public static SqlType SmallInt { get; } = new(SqlTypeName.SmallInt);
    public static SqlType Int { get; } = new(SqlTypeName.Int);
    public static SqlType BigInt { get; } = new(SqlTypeName.BigInt);
    public static SqlType SmallMoney { get; } = new(SqlTypeName.SmallMoney, 10, 4);
    public static SqlType Money { get; } = new(SqlTypeName.Money, 19, 4);
    public static SqlType Real { get; } = new(SqlTypeName.Real, 24);
    public static SqlType Date { get; } = new(SqlTypeName.Date);
    public static SqlType SmallDateTime { get; } = new(SqlTypeName.SmallDateTime, scale: 0);
    public static SqlType DateTime { get; } = new(SqlTypeName.DateTime, scale: 3);
    public static SqlType UniqueIdentifier { get; } = new(SqlTypeName.UniqueIdentifier);
    public static SqlType Text { get; } = Character(SqlTypeName.Text, null, true, null);
    public static SqlType TextWithCollation(string collation) => Character(SqlTypeName.Text, null, true, collation);
    public static SqlType NText { get; } = Character(SqlTypeName.NText, null, true, null);
    public static SqlType NTextWithCollation(string collation) => Character(SqlTypeName.NText, null, true, collation);
    public static SqlType Image { get; } = new(SqlTypeName.Image, isMax: true);
    public static SqlType Timestamp { get; } = new(SqlTypeName.Timestamp, length: 8);
    public static SqlType RowVersion => Timestamp;
    public static SqlType Xml { get; } = new(SqlTypeName.Xml, isMax: true);
    public static SqlType Json { get; } = new(SqlTypeName.Json, isMax: true);
    public static SqlType SqlVariant { get; } = new(SqlTypeName.SqlVariant);
    public static SqlType HierarchyId { get; } = new(SqlTypeName.HierarchyId, isMax: true);
    public static SqlType Geometry { get; } = new(SqlTypeName.Geometry, isMax: true);
    public static SqlType Geography { get; } = new(SqlTypeName.Geography, isMax: true);
    public static SqlType Cursor { get; } = new(SqlTypeName.Cursor);
    public static SqlType Table { get; } = new(SqlTypeName.Table);

    // Common SQL spelling aliases.
    public static SqlType Boolean => Bit;
    public static SqlType Integer => Int;
    public static SqlType Dec(byte precision = 18, byte scale = 0) => Decimal(precision, scale);
    public static SqlType DoublePrecision => Float(53);
    public static SqlType Character(int length = 1, string? collation = null) => Char(length, collation);
    public static SqlType CharacterVarying(int length = 1, string? collation = null) => VarChar(length, collation);
    public static SqlType CharacterVaryingMax(string? collation = null) => VarCharMax(collation);
    public static SqlType BinaryVarying(int length = 1) => VarBinary(length);
    public static SqlType BinaryVaryingMax => VarBinaryMax;
    public static SqlType NationalCharacter(int length = 1, string? collation = null) => NChar(length, collation);
    public static SqlType NationalChar(int length = 1, string? collation = null) => NChar(length, collation);
    public static SqlType NationalCharacterVarying(int length = 1, string? collation = null) => NVarChar(length, collation);
    public static SqlType NationalCharVarying(int length = 1, string? collation = null) => NVarChar(length, collation);
    public static SqlType NationalCharacterVaryingMax(string? collation = null) => NVarCharMax(collation);
    public static SqlType NationalCharVaryingMax(string? collation = null) => NVarCharMax(collation);
    public static SqlType NationalText => NText;
    public static SqlType SysName => NVarChar(128);

    public static SqlType Decimal(byte precision = 18, byte scale = 0)
        => Exact(SqlTypeName.Decimal, precision, scale);

    public static SqlType Numeric(byte precision = 18, byte scale = 0)
        => Exact(SqlTypeName.Numeric, precision, scale);

    public static SqlType Float(byte precision = 53)
    {
        if (precision is < 1 or > 53) throw new ArgumentOutOfRangeException(nameof(precision));
        return new SqlType(SqlTypeName.Float, precision <= 24 ? (byte)24 : (byte)53);
    }

    public static SqlType Time(byte scale = 7) => ScaledTemporal(SqlTypeName.Time, scale);
    public static SqlType DateTime2(byte scale = 7) => ScaledTemporal(SqlTypeName.DateTime2, scale);
    public static SqlType DateTimeOffset(byte scale = 7) => ScaledTemporal(SqlTypeName.DateTimeOffset, scale);

    public static SqlType Char(int length = 1, string? collation = null)
        => Character(SqlTypeName.Char, ValidateLength(length, 8_000), false, collation);

    public static SqlType VarChar(int length = 1, string? collation = null)
        => Character(SqlTypeName.VarChar, ValidateLength(length, 8_000), false, collation);

    public static SqlType VarCharMax(string? collation = null)
        => Character(SqlTypeName.VarChar, null, true, collation);

    public static SqlType NChar(int length = 1, string? collation = null)
        => Character(SqlTypeName.NChar, ValidateLength(length, 4_000), false, collation);

    public static SqlType NVarChar(int length = 1, string? collation = null)
        => Character(SqlTypeName.NVarChar, ValidateLength(length, 4_000), false, collation);

    public static SqlType NVarCharMax(string? collation = null)
        => Character(SqlTypeName.NVarChar, null, true, collation);

    public static SqlType Binary(int length = 1)
        => new(SqlTypeName.Binary, length: ValidateLength(length, 8_000));

    public static SqlType VarBinary(int length = 1)
        => new(SqlTypeName.VarBinary, length: ValidateLength(length, 8_000));

    public static SqlType VarBinaryMax { get; } = new(SqlTypeName.VarBinary, isMax: true);

    public static SqlType Vector(ushort dimensions, SqlVectorBaseType baseType = SqlVectorBaseType.Float32)
    {
        if (!Enum.IsDefined(baseType)) throw new ArgumentOutOfRangeException(nameof(baseType));
        if (dimensions is 0 or > 1_998) throw new ArgumentOutOfRangeException(nameof(dimensions));
        return new SqlType(SqlTypeName.Vector, vectorDimensions: dimensions, vectorBaseType: baseType);
    }

    public static SqlType TypedXml(SqlXmlSchemaCollection schemaCollection,
        SqlXmlContentKind contentKind = SqlXmlContentKind.Content)
    {
        ArgumentNullException.ThrowIfNull(schemaCollection);
        if (!Enum.IsDefined(contentKind)) throw new ArgumentOutOfRangeException(nameof(contentKind));
        return new SqlType(SqlTypeName.Xml, isMax: true, xmlContentKind: contentKind,
            xmlSchemaCollection: schemaCollection);
    }

    public static SqlType Alias(string schemaName, string name, SqlType baseType, bool isNullable = true)
    {
        ArgumentNullException.ThrowIfNull(baseType);
        if (baseType.IsUserDefined || baseType.Name is not (
                SqlTypeName.BigInt or SqlTypeName.Int or SqlTypeName.SmallInt or SqlTypeName.TinyInt or
                SqlTypeName.Binary or SqlTypeName.VarBinary or SqlTypeName.Bit or
                SqlTypeName.Char or SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.VarChar or
                SqlTypeName.Date or SqlTypeName.DateTime or SqlTypeName.DateTime2 or SqlTypeName.DateTimeOffset or
                SqlTypeName.SmallDateTime or SqlTypeName.Time or SqlTypeName.Decimal or SqlTypeName.Numeric or
                SqlTypeName.Float or SqlTypeName.Real or SqlTypeName.Image or SqlTypeName.Money or
                SqlTypeName.SmallMoney or SqlTypeName.SqlVariant or SqlTypeName.Text or SqlTypeName.NText or
                SqlTypeName.UniqueIdentifier))
            throw new ArgumentException("SQL Server does not allow an alias type over this system type.", nameof(baseType));
        schemaName = SqlXmlSchemaCollection.ValidateIdentifier(schemaName, nameof(schemaName));
        name = SqlXmlSchemaCollection.ValidateIdentifier(name, nameof(name));
        return new SqlType(baseType.Name, baseType.Precision, baseType.Scale, baseType.Length, baseType.IsMax,
            baseType.Collation, baseType.VectorDimensions, baseType.VectorBaseType, baseType.XmlContentKind,
            baseType.XmlSchemaCollection, SqlUserTypeKind.Alias, schemaName, name, baseType, isNullable);
    }

    public static SqlType ClrUserDefined(string schemaName, string name, string assemblyName, string className,
        SqlClrSerializationFormat serializationFormat, int maxByteSize, bool isByteOrdered = false,
        bool isFixedLength = false, string? validationMethodName = null)
    {
        if (serializationFormat == SqlClrSerializationFormat.Native)
            throw new ArgumentException("FORMAT.Native must not specify MaxByteSize; use the overload without maxByteSize.", nameof(maxByteSize));
        return CreateClrUserDefined(schemaName, name, assemblyName, className, serializationFormat, maxByteSize,
            isByteOrdered, isFixedLength, validationMethodName);
    }

    public static SqlType ClrUserDefined(string schemaName, string name, string assemblyName, string className,
        SqlClrSerializationFormat serializationFormat, bool isByteOrdered = false,
        bool isFixedLength = false, string? validationMethodName = null)
    {
        if (serializationFormat != SqlClrSerializationFormat.Native)
            throw new ArgumentException("FORMAT.UserDefined requires MaxByteSize.", nameof(serializationFormat));
        return CreateClrUserDefined(schemaName, name, assemblyName, className, serializationFormat, null,
            isByteOrdered, isFixedLength, validationMethodName);
    }

    private static SqlType CreateClrUserDefined(string schemaName, string name, string assemblyName, string className,
        SqlClrSerializationFormat serializationFormat, int? maxByteSize, bool isByteOrdered,
        bool isFixedLength, string? validationMethodName)
    {
        schemaName = SqlXmlSchemaCollection.ValidateIdentifier(schemaName, nameof(schemaName));
        name = SqlXmlSchemaCollection.ValidateIdentifier(name, nameof(name));
        assemblyName = SqlXmlSchemaCollection.ValidateIdentifier(assemblyName, nameof(assemblyName));
        className = SqlXmlSchemaCollection.ValidateIdentifier(className, nameof(className));
        if (!Enum.IsDefined(serializationFormat)) throw new ArgumentOutOfRangeException(nameof(serializationFormat));
        if (serializationFormat == SqlClrSerializationFormat.UserDefined && maxByteSize is 0 or < -1 or > 8_000)
            throw new ArgumentOutOfRangeException(nameof(maxByteSize));
        if (serializationFormat == SqlClrSerializationFormat.Native && maxByteSize is not null)
            throw new ArgumentException("FORMAT.Native cannot declare MaxByteSize.", nameof(maxByteSize));
        if (isFixedLength && serializationFormat == SqlClrSerializationFormat.UserDefined && maxByteSize < 1)
            throw new ArgumentException("A fixed-length CLR type requires a finite maximum byte size.", nameof(isFixedLength));
        if (validationMethodName is not null)
            validationMethodName = SqlXmlSchemaCollection.ValidateIdentifier(validationMethodName, nameof(validationMethodName));
        return new SqlType(SqlTypeName.VarBinary, length: maxByteSize is > 0 ? maxByteSize : null,
            isMax: maxByteSize < 0, userTypeKind: SqlUserTypeKind.Clr, userTypeSchema: schemaName,
            userTypeName: name, clrAssemblyName: assemblyName, clrClassName: className,
            clrSerializationFormat: serializationFormat, clrMaxByteSize: maxByteSize,
            clrIsByteOrdered: isByteOrdered, clrIsFixedLength: isFixedLength,
            clrValidationMethodName: validationMethodName);
    }

    public static SqlType ClrUserDefined(string schemaName, string name, string assemblyName,
        SqlClrSerializationFormat serializationFormat, int maxByteSize, bool isByteOrdered = false,
        bool isFixedLength = false, string? validationMethodName = null) =>
        ClrUserDefined(schemaName, name, assemblyName, name, serializationFormat, maxByteSize,
            isByteOrdered, isFixedLength, validationMethodName);

    public static SqlType ClrUserDefined(string schemaName, string name, string assemblyName,
        SqlClrSerializationFormat serializationFormat, bool isByteOrdered = false,
        bool isFixedLength = false, string? validationMethodName = null) =>
        ClrUserDefined(schemaName, name, assemblyName, name, serializationFormat,
            isByteOrdered, isFixedLength, validationMethodName);

    internal static SqlType Restore(SqlTypeName name, byte? precision, byte? scale, int? length, bool isMax,
        string? collation, ushort? vectorDimensions, SqlVectorBaseType? vectorBaseType,
        SqlXmlContentKind? xmlContentKind = null, SqlXmlSchemaCollection? xmlSchemaCollection = null,
        SqlUserTypeKind userTypeKind = SqlUserTypeKind.None, string? userTypeSchema = null,
        string? userTypeName = null, SqlType? baseType = null, bool? userTypeNullable = null,
        string? clrAssemblyName = null, string? clrClassName = null,
        SqlClrSerializationFormat? clrSerializationFormat = null, int? clrMaxByteSize = null,
        bool clrIsByteOrdered = false, bool clrIsFixedLength = false, string? clrValidationMethodName = null)
    {
        var result = new SqlType(name, precision, scale, length, isMax, NormalizeCollation(collation),
            vectorDimensions, vectorBaseType, xmlContentKind, xmlSchemaCollection, userTypeKind,
            userTypeSchema, userTypeName, baseType, userTypeNullable, clrAssemblyName, clrClassName,
            clrSerializationFormat, clrMaxByteSize, clrIsByteOrdered, clrIsFixedLength, clrValidationMethodName);
        result.ValidateDeclaration();
        return result;
    }

    internal SqlType RebindXmlSchemaCollection(SqlXmlSchemaCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        if (Name != SqlTypeName.Xml || XmlSchemaCollection is null || XmlContentKind is null)
            throw new InvalidOperationException("Only typed XML can be rebound to a schema collection.");
        return TypedXml(collection, XmlContentKind.Value);
    }

    internal SqlType ApplyDefaultCollation(string defaultCollation)
    {
        _ = SqlCollation.Parse(defaultCollation);
        if (UserTypeKind == SqlUserTypeKind.Alias)
            return Alias(UserTypeSchema!, UserTypeName!, BaseType!.ApplyDefaultCollation(defaultCollation), UserTypeNullable!.Value);
        if (Collation is not null) return this;
        return Name switch
        {
            SqlTypeName.Char => Char(Length!.Value, defaultCollation),
            SqlTypeName.VarChar when IsMax => VarCharMax(defaultCollation),
            SqlTypeName.VarChar => VarChar(Length!.Value, defaultCollation),
            SqlTypeName.Text => TextWithCollation(defaultCollation),
            SqlTypeName.NChar => NChar(Length!.Value, defaultCollation),
            SqlTypeName.NVarChar when IsMax => NVarCharMax(defaultCollation),
            SqlTypeName.NVarChar => NVarChar(Length!.Value, defaultCollation),
            SqlTypeName.NText => NTextWithCollation(defaultCollation),
            _ => this
        };
    }

    public void Validate(SqlValue value, string columnName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.IsNull) return;
        if (value.StorageFamily != StorageFamily)
            throw new ArgumentException($"Column '{columnName}' expects {this}, received {value.StorageFamily}.", nameof(value));

        switch (value)
        {
            case IntegerSqlValue integer:
                ValidateInteger(integer.Value, columnName);
                break;
            case DecimalSqlValue exact:
                ValidateDecimal(exact.Value, columnName);
                break;
            case FloatSqlValue approximate when !double.IsFinite(approximate.Value):
                throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' requires a finite SQL floating-point value.");
            case FloatSqlValue approximate when Name == SqlTypeName.Real &&
                                                   (approximate.Value < -float.MaxValue || approximate.Value > float.MaxValue):
                throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' exceeds real's range.");
            case TextSqlValue text:
                ValidateText(text.Value, columnName);
                break;
            case BinarySqlValue binary:
                ValidateBinary(binary.Value, columnName);
                break;
            case HierarchyIdSqlValue hierarchy when Name != SqlTypeName.HierarchyId:
                throw new ArgumentException($"Column '{columnName}' is not hierarchyid.", nameof(value));
            case HierarchyIdSqlValue hierarchy:
                ValidateBinary(hierarchy.Value.Serialize(), columnName);
                break;
            case SpatialSqlValue spatial when Name == SqlTypeName.Geometry && spatial.Value.Kind != SqlSpatialKind.Geometry ||
                                                  Name == SqlTypeName.Geography && spatial.Value.Kind != SqlSpatialKind.Geography:
                throw new ArgumentException($"Column '{columnName}' has the wrong spatial kind.", nameof(value));
            case SpatialSqlValue when Name is not (SqlTypeName.Geometry or SqlTypeName.Geography):
                throw new ArgumentException($"Column '{columnName}' is not spatial.", nameof(value));
            case TimeSqlValue time:
                ValidateTemporalScale(time.Value.Ticks, columnName);
                break;
            case DateTimeSqlValue dateTime:
                ValidateDateTime(dateTime.Value, columnName);
                break;
            case DateTimeOffsetSqlValue dateTimeOffset:
                ValidateTemporalScale(dateTimeOffset.Value.Ticks, columnName);
                break;
            case VariantSqlValue variant:
                ValidateVariant(variant, columnName);
                break;
            case VectorSqlValue vector:
                ValidateVector(vector, columnName);
                break;
        }
        try
        {
            var normalized = NormalizeForStorage(value);
            if (Name == SqlTypeName.SmallDateTime && normalized is DateTimeSqlValue small &&
                small.Value >= new DateTime(2079, 6, 7))
                throw new OverflowException();
        }
        catch (Exception exception) when (exception is OverflowException or ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value,
                $"Column '{columnName}' overflows {this} after SQL assignment rounding: {exception.Message}");
        }
    }

    internal SqlValue NormalizeForStorage(SqlValue value)
    {
        if (value.IsNull) return value;
        return value switch
        {
            DecimalSqlValue exact when Name is SqlTypeName.Money or SqlTypeName.SmallMoney =>
                RoundedDecimal(exact.Value, 4),
            DecimalSqlValue exact when Name is SqlTypeName.Decimal or SqlTypeName.Numeric =>
                RoundedDecimal(exact.Value, Scale!.Value),
            FloatSqlValue approximate when Name == SqlTypeName.Real || Name == SqlTypeName.Float && Precision <= 24 =>
                SqlValue.Float((float)approximate.Value),
            TimeSqlValue time => SqlValue.Time(new TimeOnly(RoundTicks(time.Value.Ticks, Scale!.Value) % TimeSpan.TicksPerDay)),
            DateTimeSqlValue dateTime => SqlValue.DateTime(RoundDateTimeValue(dateTime.Value)),
            DateTimeOffsetSqlValue offset => NormalizeOffset(offset.Value),
            TextSqlValue text when Name is SqlTypeName.Char or SqlTypeName.NChar => SqlValue.Text(PadText(text.Value)),
            BinarySqlValue binary when Name == SqlTypeName.Binary && binary.Value.Length < Length =>
                SqlValue.Binary(binary.Value.Span.ToArray().Concat(new byte[Length!.Value - binary.Value.Length]).ToArray()),
            _ => value
        };

        SqlValue RoundedDecimal(SqlDecimal input, byte scale)
        { input.RoundToScale(scale, out var coefficient); return SqlValue.Decimal(coefficient, scale); }

        SqlValue NormalizeOffset(System.DateTimeOffset input)
        {
            var utcTicks = RoundTicks(input.UtcTicks, Scale!.Value);
            return SqlValue.DateTimeOffset(new System.DateTimeOffset(checked(utcTicks + input.Offset.Ticks), input.Offset));
        }
    }

    /// <summary>Applies SQL assignment conversion followed by the declaration's validation and normalization.</summary>
    public SqlValue Convert(SqlValue value) => SqlConversion.ConvertTo(this, value);

    /// <summary>Applies conversion using the supplied source declaration for source-sensitive SQL rules.</summary>
    public SqlValue Convert(SqlType source, SqlValue value) => SqlConversion.ConvertTo(this, source, value);

    private string PadText(string value)
    {
        var actual = Name == SqlTypeName.NChar ? value.Length : (CollationMetadata ?? SqlCollation.Default).GetByteCount(value);
        return actual >= Length ? value : value + new string(' ', Length!.Value - actual);
    }

    private DateTime RoundDateTimeValue(DateTime value)
    {
        if (Name == SqlTypeName.SmallDateTime)
        {
            var minuteTicks = value.Ticks - value.Ticks % TimeSpan.TicksPerMinute;
            if (value.Ticks - minuteTicks >= TimeSpan.TicksPerSecond * 30) minuteTicks += TimeSpan.TicksPerMinute;
            return new DateTime(minuteTicks, DateTimeKind.Unspecified);
        }
        if (Name == SqlTypeName.DateTime)
        {
            var units = (long)Math.Round(value.TimeOfDay.TotalSeconds * 300d, MidpointRounding.AwayFromZero);
            var ticks = checked(value.Date.Ticks + (long)Math.Round(units * (double)TimeSpan.TicksPerSecond / 300d,
                MidpointRounding.AwayFromZero));
            return new DateTime(ticks, DateTimeKind.Unspecified);
        }
        return new DateTime(RoundTicks(value.Ticks, Scale!.Value), DateTimeKind.Unspecified);
    }

    private static long RoundTicks(long ticks, byte scale)
    {
        var quantum = (long)Math.Pow(10, 7 - scale); var remainder = ticks % quantum;
        return remainder == 0 ? ticks : checked(ticks - remainder + (remainder * 2 >= quantum ? quantum : 0));
    }

    public override string ToString()
    {
        if (IsUserDefined) return QualifiedUserTypeName!;
        return Name switch
        {
            SqlTypeName.Decimal or SqlTypeName.Numeric => $"{Name.ToString().ToLowerInvariant()}({Precision},{Scale})",
            SqlTypeName.Float => $"float({Precision})",
            SqlTypeName.Time or SqlTypeName.DateTime2 or SqlTypeName.DateTimeOffset =>
                $"{Name.ToString().ToLowerInvariant()}({Scale})",
            SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.NChar or SqlTypeName.NVarChar or
                SqlTypeName.Binary or SqlTypeName.VarBinary =>
                $"{Name.ToString().ToLowerInvariant()}({(IsMax ? "max" : Length)})",
            SqlTypeName.Vector => $"vector({VectorDimensions},{VectorBaseType?.ToString().ToLowerInvariant()})",
            SqlTypeName.Xml when XmlSchemaCollection is not null =>
                $"xml({XmlContentKind!.Value.ToString().ToLowerInvariant()} {XmlSchemaCollection.QualifiedName})",
            _ => Name.ToString().ToLowerInvariant()
        };
    }

    private static SqlType Exact(SqlTypeName name, byte precision, byte scale)
    {
        if (precision is < 1 or > 38) throw new ArgumentOutOfRangeException(nameof(precision));
        if (scale > precision) throw new ArgumentOutOfRangeException(nameof(scale));
        return new SqlType(name, precision, scale);
    }

    private static SqlType ScaledTemporal(SqlTypeName name, byte scale)
    {
        if (scale > 7) throw new ArgumentOutOfRangeException(nameof(scale));
        return new SqlType(name, scale: scale);
    }

    private static SqlType Character(SqlTypeName name, int? length, bool isMax, string? collation)
        => new(name, length: length, isMax: isMax, collation: NormalizeCollation(collation));

    private static int ValidateLength(int length, int maximum)
    {
        if (length < 1 || length > maximum) throw new ArgumentOutOfRangeException(nameof(length));
        return length;
    }

    private static string? NormalizeCollation(string? collation)
    {
        if (collation is null) return null;
        if (string.IsNullOrWhiteSpace(collation)) throw new ArgumentException("Collation cannot be empty.", nameof(collation));
        if (collation.Length > 128) throw new ArgumentOutOfRangeException(nameof(collation),
            "SQL Server collation names cannot exceed 128 characters.");
        return collation;
    }

    private void ValidateDeclaration()
    {
        if (UserTypeKind == SqlUserTypeKind.Alias)
        {
            if (BaseType is null || BaseType.IsUserDefined || UserTypeNullable is null ||
                string.IsNullOrWhiteSpace(UserTypeSchema) || string.IsNullOrWhiteSpace(UserTypeName)) throw Invalid();
            if (Alias(UserTypeSchema, UserTypeName, BaseType, UserTypeNullable.Value) != this) throw Invalid();
            return;
        }
        if (UserTypeKind == SqlUserTypeKind.Clr)
        {
            if (ClrSerializationFormat is null ||
                string.IsNullOrWhiteSpace(UserTypeSchema) || string.IsNullOrWhiteSpace(UserTypeName) ||
                string.IsNullOrWhiteSpace(ClrAssemblyName) || string.IsNullOrWhiteSpace(ClrClassName)) throw Invalid();
            var canonicalClr = ClrSerializationFormat == SqlClrSerializationFormat.Native
                ? ClrUserDefined(UserTypeSchema, UserTypeName, ClrAssemblyName, ClrClassName,
                    ClrSerializationFormat.Value, ClrIsByteOrdered, ClrIsFixedLength, ClrValidationMethodName)
                : ClrUserDefined(UserTypeSchema, UserTypeName, ClrAssemblyName, ClrClassName,
                    ClrSerializationFormat.Value, ClrMaxByteSize ?? throw Invalid(), ClrIsByteOrdered, ClrIsFixedLength,
                    ClrValidationMethodName);
            if (canonicalClr != this)
                throw Invalid();
            return;
        }
        if (UserTypeKind != SqlUserTypeKind.None || BaseType is not null || UserTypeNullable is not null ||
            UserTypeSchema is not null || UserTypeName is not null || ClrAssemblyName is not null ||
            ClrClassName is not null || ClrSerializationFormat is not null || ClrMaxByteSize is not null ||
            ClrIsByteOrdered || ClrIsFixedLength || ClrValidationMethodName is not null) throw Invalid();
        var canonical = Name switch
        {
            SqlTypeName.Decimal => Decimal(Precision ?? throw Invalid(), Scale ?? throw Invalid()),
            SqlTypeName.Numeric => Numeric(Precision ?? throw Invalid(), Scale ?? throw Invalid()),
            SqlTypeName.Float => Float(Precision ?? throw Invalid()),
            SqlTypeName.Time => Time(Scale ?? throw Invalid()),
            SqlTypeName.DateTime2 => DateTime2(Scale ?? throw Invalid()),
            SqlTypeName.DateTimeOffset => DateTimeOffset(Scale ?? throw Invalid()),
            SqlTypeName.Char => Char(Length ?? throw Invalid(), Collation),
            SqlTypeName.VarChar when IsMax => VarCharMax(Collation),
            SqlTypeName.VarChar => VarChar(Length ?? throw Invalid(), Collation),
            SqlTypeName.NChar => NChar(Length ?? throw Invalid(), Collation),
            SqlTypeName.NVarChar when IsMax => NVarCharMax(Collation),
            SqlTypeName.NVarChar => NVarChar(Length ?? throw Invalid(), Collation),
            SqlTypeName.Binary => Binary(Length ?? throw Invalid()),
            SqlTypeName.VarBinary when IsMax => VarBinaryMax,
            SqlTypeName.VarBinary => VarBinary(Length ?? throw Invalid()),
            SqlTypeName.Vector => Vector(VectorDimensions ?? throw Invalid(), VectorBaseType ?? throw Invalid()),
            SqlTypeName.Text => Collation is null ? Text : TextWithCollation(Collation),
            SqlTypeName.NText => Collation is null ? NText : NTextWithCollation(Collation),
            SqlTypeName.Xml when XmlSchemaCollection is not null && XmlContentKind is not null =>
                TypedXml(XmlSchemaCollection, XmlContentKind.Value),
            _ => CanonicalSimple(Name)
        };
        if (canonical != this) throw Invalid();
    }

    private static SqlType CanonicalSimple(SqlTypeName name) => name switch
    {
        SqlTypeName.Bit => Bit, SqlTypeName.TinyInt => TinyInt, SqlTypeName.SmallInt => SmallInt,
        SqlTypeName.Int => Int, SqlTypeName.BigInt => BigInt, SqlTypeName.SmallMoney => SmallMoney,
        SqlTypeName.Money => Money, SqlTypeName.Real => Real, SqlTypeName.Date => Date,
        SqlTypeName.SmallDateTime => SmallDateTime, SqlTypeName.DateTime => DateTime,
        SqlTypeName.UniqueIdentifier => UniqueIdentifier,
        SqlTypeName.Image => Image, SqlTypeName.RowVersion => Timestamp, SqlTypeName.Timestamp => Timestamp,
        SqlTypeName.Xml => Xml,
        SqlTypeName.Json => Json, SqlTypeName.SqlVariant => SqlVariant, SqlTypeName.HierarchyId => HierarchyId,
        SqlTypeName.Geometry => Geometry, SqlTypeName.Geography => Geography, SqlTypeName.Cursor => Cursor,
        SqlTypeName.Table => Table,
        _ => throw Invalid()
    };

    private static ArgumentException Invalid() => new("Invalid SQL type facet combination.");

    private void ValidateInteger(long value, string columnName)
    {
        var valid = Name switch
        {
            SqlTypeName.TinyInt => value is >= byte.MinValue and <= byte.MaxValue,
            SqlTypeName.SmallInt => value is >= short.MinValue and <= short.MaxValue,
            SqlTypeName.Int => value is >= int.MinValue and <= int.MaxValue,
            SqlTypeName.BigInt => true,
            _ => false
        };
        if (!valid) throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' exceeds {Name}'s range.");
    }

    private void ValidateDecimal(SqlDecimal value, string columnName)
    {
        var precision = Precision ?? throw new InvalidOperationException();
        var scale = Scale ?? throw new InvalidOperationException();
        if (Name is SqlTypeName.Money or SqlTypeName.SmallMoney)
        {
            value.RoundToScale(4, out var scaled);
            var limit = Name == SqlTypeName.Money ? new BigInteger(long.MaxValue) : new BigInteger(int.MaxValue);
            var minimum = Name == SqlTypeName.Money ? new BigInteger(long.MinValue) : new BigInteger(int.MinValue);
            if (scaled < minimum || scaled > limit)
                throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' exceeds {Name}'s range.");
            return;
        }
        value.RoundToScale(scale, out var coefficient);
        if (SqlDecimal.CountDigits(BigInteger.Abs(coefficient)) > precision)
            throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' does not fit {this}.");
    }

    private void ValidateText(string value, string columnName)
    {
        if (Name == SqlTypeName.Xml)
        {
            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    ConformanceLevel = XmlContentKind == SqlXmlContentKind.Document
                        ? ConformanceLevel.Document : ConformanceLevel.Fragment
                };
                if (XmlSchemaCollection is not null)
                {
                    settings.Schemas = XmlSchemaCollection.CreateSchemaSet();
                    settings.ValidationType = ValidationType.Schema;
                    settings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings;
                }
                using var reader = XmlReader.Create(new StringReader(value), settings);
                while (reader.Read()) { }
            }
            catch (Exception exception) when (exception is XmlException or XmlSchemaException)
            {
                var constraint = XmlSchemaCollection is null
                    ? "well-formed XML"
                    : $"XML valid for {XmlSchemaCollection.QualifiedName}";
                throw new ArgumentException($"Column '{columnName}' requires {constraint}.", nameof(value), exception);
            }
            return;
        }
        if (Name == SqlTypeName.Json)
        {
            try
            {
                using var document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = 128 });
                if (document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
                    throw new ArgumentException($"Column '{columnName}' requires a JSON object or array.", nameof(value));
                ValidateJsonLimits(document.RootElement, columnName);
            }
            catch (JsonException exception)
            {
                throw new ArgumentException($"Column '{columnName}' requires valid JSON.", nameof(value), exception);
            }
            return;
        }
        if (IsMax || Name is SqlTypeName.Text or SqlTypeName.NText) return;
        int actual;
        try
        {
            actual = Name is SqlTypeName.NChar or SqlTypeName.NVarChar
                ? value.Length
                : (CollationMetadata ?? SqlCollation.Default).GetByteCount(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException($"Column '{columnName}' contains text that is not representable in " +
                $"collation '{Collation ?? SqlCollation.Default.Name}'.", nameof(value), exception);
        }
        if (actual > Length)
            throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' exceeds {this}'s length.");
    }

    private static void ValidateJsonLimits(JsonElement root, string columnName)
    {
        const int maximumUniqueKeys = 32 * 1_024;
        const int maximumCollectionItems = ushort.MaxValue;
        const int maximumKeyBytes = 7_998;
        const int maximumStringBytes = 536_870_911;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<JsonElement>(); pending.Push(root);
        while (pending.Count != 0)
        {
            var element = pending.Pop();
            if (element.ValueKind == JsonValueKind.Object)
            {
                var count = 0;
                foreach (var property in element.EnumerateObject())
                {
                    if (++count > maximumCollectionItems)
                        throw new ArgumentOutOfRangeException(nameof(root), $"Column '{columnName}' has more than 65,535 properties in one JSON object.");
                    if (Encoding.UTF8.GetByteCount(property.Name) > maximumKeyBytes)
                        throw new ArgumentOutOfRangeException(nameof(root), $"Column '{columnName}' contains a JSON key longer than 7,998 UTF-8 bytes.");
                    if (keys.Add(property.Name) && keys.Count > maximumUniqueKeys)
                        throw new ArgumentOutOfRangeException(nameof(root), $"Column '{columnName}' contains more than 32K unique JSON keys.");
                    pending.Push(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                if (element.GetArrayLength() > maximumCollectionItems)
                    throw new ArgumentOutOfRangeException(nameof(root), $"Column '{columnName}' has more than 65,535 elements in one JSON array.");
                foreach (var item in element.EnumerateArray()) pending.Push(item);
            }
            else if (element.ValueKind == JsonValueKind.String &&
                     Encoding.UTF8.GetByteCount(element.GetString()!) > maximumStringBytes)
                throw new ArgumentOutOfRangeException(nameof(root), $"Column '{columnName}' contains an oversized JSON string value.");
        }
    }

    private void ValidateBinary(ReadOnlyMemory<byte> value, string columnName)
    {
        var length = value.Length;
        if (UserTypeKind == SqlUserTypeKind.Clr)
        {
            if (ClrIsFixedLength && ClrMaxByteSize is { } fixedSize && length != fixedSize)
                throw new ArgumentException($"Column '{columnName}' requires exactly {ClrMaxByteSize} serialized bytes.", nameof(length));
            if (ClrMaxByteSize is >= 0 && length > ClrMaxByteSize)
                throw new ArgumentOutOfRangeException(nameof(length),
                    $"Column '{columnName}' exceeds the CLR UDT maximum serialized size.");
            if (ClrSerializationFormat == SqlClrSerializationFormat.Native && length > 8_000)
                throw new ArgumentOutOfRangeException(nameof(length), "Native CLR UDT serialization cannot exceed 8,000 bytes.");
            if (SqlClrRuntime.TryValidate(this, value, out var valid) && !valid)
                throw new ArgumentException($"Column '{columnName}' failed CLR UDT validation.", nameof(value));
            return;
        }
        if (Name == SqlTypeName.HierarchyId && length > 892)
            throw new ArgumentOutOfRangeException(nameof(length),
                $"Column '{columnName}' exceeds hierarchyid's 892-byte representation limit.");
        if (Name == SqlTypeName.HierarchyId) _ = SqlHierarchyId.Deserialize(value.Span);
        if (Name == SqlTypeName.Geometry) _ = SqlSpatial.Deserialize(value.Span, SqlSpatialKind.Geometry);
        if (Name == SqlTypeName.Geography) _ = SqlSpatial.Deserialize(value.Span, SqlSpatialKind.Geography);
        if (Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp && length != 8)
            throw new ArgumentException($"Column '{columnName}' requires exactly 8 bytes.", nameof(length));
        if (!IsMax && Length is { } maximum && length > maximum)
            throw new ArgumentOutOfRangeException(nameof(length), $"Column '{columnName}' exceeds {this}'s length.");
    }

    private void ValidateTemporalScale(long ticks, string columnName)
    {
        _ = Scale ?? throw new InvalidOperationException();
    }

    private void ValidateDateTime(DateTime value, string columnName)
    {
        switch (Name)
        {
            case SqlTypeName.SmallDateTime:
                if (value < new DateTime(1900, 1, 1) || value >= new DateTime(2079, 6, 7))
                    throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' is not a valid smalldatetime.");
                break;
            case SqlTypeName.DateTime:
                if (value < new DateTime(1753, 1, 1))
                    throw new ArgumentOutOfRangeException(nameof(value), $"Column '{columnName}' is not a valid datetime.");
                break;
            case SqlTypeName.DateTime2:
                ValidateTemporalScale(value.Ticks, columnName);
                break;
        }
    }

    private static void ValidateVariant(VariantSqlValue variant, string columnName)
    {
        if (!variant.DeclaredType.CanBeSqlVariantValue ||
            variant.Value switch { TextSqlValue text =>
                    (variant.DeclaredType.Name is SqlTypeName.NChar or SqlTypeName.NVarChar
                        ? checked(text.Value.Length * 2)
                        : (variant.DeclaredType.CollationMetadata ?? SqlCollation.Default).GetByteCount(text.Value)) > 8_000,
                BinarySqlValue binary => binary.Value.Length > 8_000, _ => false })
            throw new ArgumentException($"Column '{columnName}' contains a value which SQL Server sql_variant cannot store.");
    }

    private void ValidateVector(VectorSqlValue vector, string columnName)
    {
        if (vector.BaseType != VectorBaseType || vector.Values.Count != VectorDimensions)
            throw new ArgumentException($"Column '{columnName}' expects {this}.", nameof(vector));
    }
}

/// <summary>An exact base-10 value supporting SQL Server's full 38-digit decimal range.</summary>
public readonly record struct SqlDecimal : IComparable<SqlDecimal>
{
    public SqlDecimal(BigInteger coefficient, byte scale)
    {
        if (scale > 38) throw new ArgumentOutOfRangeException(nameof(scale));
        if (CountDigits(BigInteger.Abs(coefficient)) > 38) throw new ArgumentOutOfRangeException(nameof(coefficient));
        Coefficient = coefficient;
        Scale = scale;
    }

    public BigInteger Coefficient { get; }
    public byte Scale { get; }

    public static SqlDecimal FromDecimal(decimal value)
    {
        var bits = decimal.GetBits(value);
        var magnitude = ((BigInteger)(uint)bits[2] << 64) | ((BigInteger)(uint)bits[1] << 32) | (uint)bits[0];
        if ((bits[3] & int.MinValue) != 0) magnitude = -magnitude;
        return new SqlDecimal(magnitude, checked((byte)((bits[3] >> 16) & 0xff)));
    }

    public static SqlDecimal Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var negative = value.StartsWith("-", StringComparison.Ordinal);
        var unsigned = value.TrimStart('+', '-');
        var point = unsigned.IndexOf('.');
        var scale = point < 0 ? 0 : unsigned.Length - point - 1;
        var digits = point < 0 ? unsigned : unsigned.Remove(point, 1);
        if (scale > 38 || !BigInteger.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var coefficient))
            throw new FormatException("Value is not a SQL decimal literal.");
        return new SqlDecimal(negative ? -coefficient : coefficient, checked((byte)scale));
    }

    public bool TryRescale(byte scale, out BigInteger coefficient)
    {
        if (scale >= Scale)
        {
            coefficient = Coefficient * BigInteger.Pow(10, scale - Scale);
            return true;
        }
        var divisor = BigInteger.Pow(10, Scale - scale);
        coefficient = BigInteger.DivRem(Coefficient, divisor, out var remainder);
        return remainder.IsZero;
    }

    public void RoundToScale(byte scale, out BigInteger coefficient)
    {
        if (scale >= Scale)
        {
            coefficient = Coefficient * BigInteger.Pow(10, scale - Scale);
            return;
        }
        var divisor = BigInteger.Pow(10, Scale - scale);
        coefficient = BigInteger.DivRem(Coefficient, divisor, out var remainder);
        if (BigInteger.Abs(remainder) * 2 >= divisor) coefficient += Coefficient.Sign;
    }

    public int CompareTo(SqlDecimal other)
    {
        var scale = Math.Max(Scale, other.Scale);
        TryRescale(scale, out var left);
        other.TryRescale(scale, out var right);
        return left.CompareTo(right);
    }

    public bool Equals(SqlDecimal other) => CompareTo(other) == 0;

    public override int GetHashCode()
    {
        var coefficient = Coefficient;
        var scale = Scale;
        while (scale > 0 && coefficient % 10 == 0)
        {
            coefficient /= 10;
            scale--;
        }
        return HashCode.Combine(coefficient, scale);
    }

    public override string ToString()
    {
        var negative = Coefficient.Sign < 0;
        var digits = BigInteger.Abs(Coefficient).ToString(CultureInfo.InvariantCulture).PadLeft(Scale + 1, '0');
        if (Scale > 0) digits = digits.Insert(digits.Length - Scale, ".");
        return negative ? "-" + digits : digits;
    }

    internal static int CountDigits(BigInteger value) => value.IsZero ? 1 : value.ToString(CultureInfo.InvariantCulture).Length;
}
