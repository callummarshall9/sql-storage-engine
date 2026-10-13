// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogDefinition
{
    internal sql_storage_engine.Models.Security.StorageSecurityState? security;
    internal sql_storage_engine.Models.Security.StorageSecurityState Security { get => security!; set => security = value; }
    public IReadOnlyList<CatalogTable> Tables { get => tables!; internal set => tables = value; }
    public IReadOnlyList<CatalogIndex> Indexes { get => indexes!; internal set => indexes = value; }
    public IReadOnlyList<CatalogScalarType> ScalarTypes { get => scalarTypes!; internal set => scalarTypes = value; }
    public IReadOnlyList<CatalogTableType> TableTypes { get => tableTypes!; internal set => tableTypes = value; }
    public IReadOnlyList<SqlXmlSchemaCollection> XmlSchemaCollections { get => xmlSchemaCollections!; internal set => xmlSchemaCollections = value; }
    public IReadOnlyList<CatalogAssembly> Assemblies { get => assemblies!; internal set => assemblies = value; }
    internal IEnumerable<CatalogTable> TablesSource { get => tablesSource!; init => tablesSource = value; }
    internal IEnumerable<CatalogIndex> IndexesSource { get => indexesSource!; init => indexesSource = value; }

    private IReadOnlyList<CatalogTable>? tables;
    private IReadOnlyList<CatalogIndex>? indexes;
    private IReadOnlyList<CatalogScalarType>? scalarTypes;
    private IReadOnlyList<CatalogTableType>? tableTypes;
    private IReadOnlyList<SqlXmlSchemaCollection>? xmlSchemaCollections;
    private IReadOnlyList<CatalogAssembly>? assemblies;
    private IEnumerable<CatalogTable>? tablesSource;
    private IEnumerable<CatalogIndex>? indexesSource;
    internal IEnumerable<CatalogScalarType>? ScalarTypesSource { get; init; }
    internal IEnumerable<CatalogTableType>? TableTypesSource { get; init; }
    internal IEnumerable<SqlXmlSchemaCollection>? XmlSchemaCollectionsSource { get; init; }
    internal IEnumerable<CatalogAssembly>? AssembliesSource { get; init; }
}