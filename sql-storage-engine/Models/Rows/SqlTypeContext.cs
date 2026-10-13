// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Numerics;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
internal sealed record SqlTypeContext
{
    public sql_storage_engine.Models.Catalog.CatalogScalarType? CatalogScalarTypeResult { get; init; }
    public SqlType[]? SqlTypes { get; init; }
    public SqlType? SqlTypeSource { get; init; }
    public SqlType? SqlTypeResult { get; init; }
    public SqlValue? SqlValueResult { get; init; }
    public SqlCollation? SqlCollationResult { get; init; }
    public string? TextResult { get; init; }
    public bool BooleanResult { get; init; }
    public SqlStorageFamily StorageFamilyResult { get; init; }
    public string? InputstringCollation { get; init; }
    public byte? InputbytePrecision { get; init; }
    public byte? InputbyteScale { get; init; }
    public int? InputintLength { get; init; }
    public ushort InputushortDimensions { get; init; }
    public SqlVectorBaseType InputSqlVectorBaseTypeBaseType { get; init; }
    public SqlXmlSchemaCollection? InputSqlXmlSchemaCollectionSchemaCollection { get; init; }
    public SqlXmlContentKind InputSqlXmlContentKindContentKind { get; init; }
    public string? InputstringSchemaName { get; init; }
    public string? InputstringName { get; init; }
    public SqlType? InputSqlTypeBaseType { get; init; }
    public bool InputboolIsNullable { get; init; }
    public string? InputstringAssemblyName { get; init; }
    public string? InputstringClassName { get; init; }
    public SqlClrSerializationFormat InputSqlClrSerializationFormatSerializationFormat { get; init; }
    public int InputintMaxByteSize { get; init; }
    public bool InputboolIsByteOrdered { get; init; }
    public bool InputboolIsFixedLength { get; init; }
    public string? InputstringValidationMethodName { get; init; }
    public SqlTypeName InputSqlTypeNameName { get; init; }
    public bool InputboolIsMax { get; init; }
    public ushort? InputushortVectorDimensions { get; init; }
    public SqlVectorBaseType? InputSqlVectorBaseTypeVectorBaseType { get; init; }
    public SqlXmlContentKind? InputSqlXmlContentKindXmlContentKind { get; init; }
    public SqlXmlSchemaCollection? InputSqlXmlSchemaCollectionXmlSchemaCollection { get; init; }
    public SqlUserTypeKind InputSqlUserTypeKindUserTypeKind { get; init; }
    public string? InputstringUserTypeSchema { get; init; }
    public string? InputstringUserTypeName { get; init; }
    public bool? InputboolUserTypeNullable { get; init; }
    public string? InputstringClrAssemblyName { get; init; }
    public string? InputstringClrClassName { get; init; }
    public SqlClrSerializationFormat? InputSqlClrSerializationFormatClrSerializationFormat { get; init; }
    public int? InputintClrMaxByteSize { get; init; }
    public bool InputboolClrIsByteOrdered { get; init; }
    public bool InputboolClrIsFixedLength { get; init; }
    public string? InputstringClrValidationMethodName { get; init; }
    public SqlXmlSchemaCollection? InputSqlXmlSchemaCollectionCollection { get; init; }
    public string? InputstringDefaultCollation { get; init; }
    public SqlValue? InputSqlValueValue { get; init; }
    public string? InputstringColumnName { get; init; }
    public SqlType? InputSqlTypeSource { get; init; }
    public SqlDecimal? DecimalSource { get; init; }
    public SqlDecimal? DecimalResult { get; init; }
    public BigInteger CoefficientSource { get; init; }
    public BigInteger CoefficientResult { get; init; }
    public byte DecimalScale { get; init; }
    public float[]? VectorSource { get; init; }
    public float[]? VectorResult { get; init; }
    public byte[]? BinaryResult { get; init; }
    public SqlHierarchyId? HierarchySource { get; init; }
    public SqlHierarchyId? HierarchyResult { get; init; }
    public SqlSpatial? SpatialResult { get; init; }
    public byte[]? BinarySource { get; init; }
    public SqlSpatial? SpatialSource { get; init; }
    public sql_storage_engine.Models.Rows.SqlSpatial[]? SpatialSources { get; init; }
    public double? DistanceResult { get; init; }
}