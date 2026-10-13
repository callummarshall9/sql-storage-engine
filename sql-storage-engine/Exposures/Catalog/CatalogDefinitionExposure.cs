// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Processings.CatalogMetadata;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogDefinitionExposure
{
    private static readonly ICatalogAssemblyCatalogDefinitionProcessingService service = new CatalogAssemblyCatalogDefinitionProcessingService(assembliesMetadata: new sql_storage_engine.Services.Foundations.CatalogAssemblies.CatalogAssemblyService(metadata: new sql_storage_engine.Brokers.Catalog.AssemblyMetadataBroker()));
    public static CatalogDefinition CreateCatalogDefinition(IEnumerable<CatalogTable> tables, IEnumerable<CatalogIndex> indexes, IEnumerable<CatalogScalarType>? scalarTypes = null, IEnumerable<CatalogTableType>? tableTypes = null, IEnumerable<SqlXmlSchemaCollection>? xmlSchemaCollections = null, IEnumerable<CatalogAssembly>? assemblies = null) =>
        service.PrepareCatalogDefinition(catalogDefinition: new CatalogDefinition { TablesSource = tables, IndexesSource = indexes, ScalarTypesSource = scalarTypes, TableTypesSource = tableTypes, XmlSchemaCollectionsSource = xmlSchemaCollections, AssembliesSource = assemblies });

    public static CatalogDefinition PrepareCatalogDefinition(CatalogDefinition catalogDefinition) =>
        service.PrepareCatalogDefinition(catalogDefinition: catalogDefinition);
}