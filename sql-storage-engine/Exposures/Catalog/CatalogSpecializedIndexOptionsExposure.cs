// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Orchestrations.SpecializedIndexOptions;
using sql_storage_engine.Services.Processings.SpecializedIndexOptions;
using sql_storage_engine.Services.Processings.CatalogFullTexts;
using sql_storage_engine.Services.Foundations.SpecializedIndexPaths;
using sql_storage_engine.Services.Foundations.SqlCollations;
using sql_storage_engine.Brokers.SpecializedIndexPaths;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogSpecializedIndexOptionsExposure
{
    private static readonly ICatalogSpecializedIndexOptionsOrchestrationService service = new CatalogSpecializedIndexOptionsOrchestrationService(specialized: new SpecializedIndexPathCatalogSpecializedIndexOptionsProcessingService(paths: new SpecializedIndexPathService(broker: new SpecializedIndexPathBroker())), fullTexts: new SqlCollationCatalogFullTextIndexOptionsProcessingService(collations: new SqlCollationService()));
    public static CatalogSpecializedIndexOptions CreateJsonCatalogSpecializedIndexOptions(params string[] paths) =>
        service.PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Json, PathSource = paths, RequirePaths = true });

    public static CatalogSpecializedIndexOptions CreateSpatialCatalogSpecializedIndexOptions() =>
        service.PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Spatial });

    public static CatalogSpecializedIndexOptions CreateSpatialCatalogSpecializedIndexOptions(int srid) =>
        service.PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Spatial, SpatialSrid = srid });

    public static CatalogSpecializedIndexOptions CreateVectorCatalogSpecializedIndexOptions(SqlVectorDistanceMetric metric = SqlVectorDistanceMetric.Cosine) =>
        service.PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Vector, VectorMetric = metric });

    public static CatalogSpecializedIndexOptions CreateFullTextCatalogSpecializedIndexOptions(CatalogFullTextIndexOptions catalogFullTextIndexOptions) =>
        service.PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.FullText, FullTextOptions = catalogFullTextIndexOptions });

    public static CatalogSpecializedIndexOptions CreateXmlCatalogSpecializedIndexOptions(CatalogXmlIndexKind kind = CatalogXmlIndexKind.Primary, params string[] paths) =>
        service.PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Xml, XmlIndexKind = kind, PathSource = paths, RequirePaths = true });

    public static CatalogSpecializedIndexOptions CreateXmlCatalogSpecializedIndexOptions(CatalogXmlIndexKind kind, IReadOnlyDictionary<string, string> namespaces, params string[] paths) =>
        service.PrepareCatalogSpecializedIndexOptions(catalogSpecializedIndexOptions: new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Xml, XmlIndexKind = kind, PathSource = paths, RequirePaths = true, NamespaceSource = namespaces, RequireNamespaces = true });
}