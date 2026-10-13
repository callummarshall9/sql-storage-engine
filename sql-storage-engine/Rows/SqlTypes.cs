// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
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