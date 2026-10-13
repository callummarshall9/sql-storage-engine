// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Processings.CatalogMetadata;

namespace sql_storage_engine.Exposures.Catalog;
public static class CatalogCheckConstraintExposure
{
    private static readonly ICatalogCheckConstraintProcessingService service = new CatalogCheckConstraintProcessingService();
    public static CatalogCheckConstraint CreateCatalogCheckConstraint(string name, string expression) =>
        service.PrepareCatalogCheckConstraint(catalogCheckConstraint: new CatalogCheckConstraint { Name = name, Expression = expression });

    public static CatalogCheckConstraint PrepareCatalogCheckConstraint(CatalogCheckConstraint catalogCheckConstraint) =>
        service.PrepareCatalogCheckConstraint(catalogCheckConstraint: catalogCheckConstraint);
}