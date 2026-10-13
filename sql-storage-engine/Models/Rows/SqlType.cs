// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
public sealed record SqlType
{
    public SqlTypeName Name { get; init; }
    public byte? Precision { get; init; }
    public byte? Scale { get; init; }
    public int? Length { get; init; }
    public bool IsMax { get; init; }
    public string? Collation { get; init; }
    public ushort? VectorDimensions { get; init; }
    public SqlVectorBaseType? VectorBaseType { get; init; }
    public SqlXmlContentKind? XmlContentKind { get; init; }
    public SqlXmlSchemaCollection? XmlSchemaCollection { get; init; }
    public SqlUserTypeKind UserTypeKind { get; init; }
    public string? UserTypeSchema { get; init; }
    public string? UserTypeName { get; init; }
    public SqlType? BaseType { get; init; }
    public bool? UserTypeNullable { get; init; }
    public string? ClrAssemblyName { get; init; }
    public string? ClrClassName { get; init; }
    public SqlClrSerializationFormat? ClrSerializationFormat { get; init; }
    public int? ClrMaxByteSize { get; init; }
    public bool ClrIsByteOrdered { get; init; }
    public bool ClrIsFixedLength { get; init; }
    public string? ClrValidationMethodName { get; init; }
}