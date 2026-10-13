// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

namespace sql_storage_engine.UnitTests;
public sealed class SpatialIndexSridTests
{
    [Test]
    public void SpatialOptionsDistinguishAllAndExactSridPolicies()
    {
        sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateSpatialCatalogSpecializedIndexOptions().SpatialSrid.Should().BeNull();
        sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateSpatialCatalogSpecializedIndexOptions(4326).SpatialSrid.Should().Be(4326);
    }

    [Test]
    public async Task ExactSridIndexRejectsIncompatibleRowsAndQueriesAndPersistsCapability()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatial-srid-{Guid.NewGuid():N}.db");
        try
        {
            RowId matchingRowId;
            await using (var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var table = await engine.CreateCatalogTableAsync("places", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "point", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeGeometry(), true)]);
                var storage = await engine.OpenTableAsync(table.Id);
                var otherRowId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateGeometrySqlValue("POINT (0 0)",0)]));
                matchingRowId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateGeometrySqlValue("POINT (1 1)",4326)]));
                await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()]));
                await engine.CreateCatalogIndexAsync("places_pk", table.Id, true, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)], sql_storage_engine.Exposures.Catalog.CatalogBTreeIndexOptionsExposure.CreateCatalogBTreeIndexOptions(CatalogIndexStorageKind.Clustered,isPrimaryKey: true));
                var buildFailure = await ((Func<Task>)(async () => await engine.CreateSpecializedCatalogIndexAsync("places_spatial_4326", table.Id, sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.First), sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateSpatialCatalogSpecializedIndexOptions(4326)))).Should().ThrowAsync<IndexBuildException>();
                buildFailure.Which.InnerException.Should().BeAssignableTo<ArgumentException>().Which.ParamName.Should().Be("row");
                await storage.UpdateAsync(otherRowId, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateGeometrySqlValue("POINT (2 2)",4326) }]));
                var definition = await engine.CreateSpecializedCatalogIndexAsync("places_spatial_4326", table.Id, sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.First), sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateSpatialCatalogSpecializedIndexOptions(4326));
                var index = await engine.OpenIndexAsync(definition.Id);
                definition.SpecializedOptions!.SpatialSrid.Should().Be(4326);
                (await index.SearchNearestAsync(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateGeometrySqlValue("POINT (0 0)",4326), 5)).Select(match => match.RowId).Should().Equal(matchingRowId, otherRowId);
                await ((Func<Task>)(async () => await index.SearchNearestAsync(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateGeometrySqlValue("POINT (0 0)",0), 1))).Should().ThrowAsync<ArgumentException>().WithParameterName("query");
                var insertionFailure = await ((Func<Task>)(async () => await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(4), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateGeometrySqlValue("POINT (3 3)",0)])))).Should().ThrowAsync<TableMutationException>();
                insertionFailure.Which.InnerException.Should().BeAssignableTo<ArgumentException>().Which.ParamName.Should().Be("row");
            }

            await using var reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var persisted = reopened.ReadCatalog().ReadIndexes().Single(index => index.Method == CatalogIndexMethod.Spatial);
            persisted.SpecializedOptions!.SpatialSrid.Should().Be(4326);
            var reopenedIndex = await reopened.OpenIndexAsync(persisted.Id);
            (await reopenedIndex.SearchNearestAsync(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateGeometrySqlValue("POINT (0 0)",4326), 5)).Should().HaveCount(2);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}