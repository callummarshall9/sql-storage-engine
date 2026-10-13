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
public sealed class VectorIndexDistanceTests
{
    [Test]
    public async Task CosineIndexRejectsZeroBuildMutationAndQueryAndRemainsReusable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vector-distance-{Guid.NewGuid():N}.db");
        try
        {
            await using var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var table = await engine.CreateCatalogTableAsync("vectors", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "embedding", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(3), true)]);
            await engine.CreateCatalogIndexAsync("vectors_pk", table.Id, true, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)], sql_storage_engine.Exposures.Catalog.CatalogBTreeIndexOptionsExposure.CreateCatalogBTreeIndexOptions(CatalogIndexStorageKind.Clustered,isPrimaryKey: true));
            var storage = await engine.OpenTableAsync(table.Id);
            var zeroRowId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(0), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([0f, 0f, 0f])]));
            for (var id = 1; id < 100; id++)
            {
                await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(id), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([(float)id, 1f, -1f])]));
            }

            var buildFailure = await ((Func<Task>)(async () => await engine.CreateSpecializedCatalogIndexAsync("vectors_cosine", table.Id, sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.Last), sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateVectorCatalogSpecializedIndexOptions(SqlVectorDistanceMetric.Cosine)))).Should().ThrowAsync<IndexBuildException>();
            buildFailure.Which.InnerException.Should().BeAssignableTo<ArgumentException>().Which.ParamName.Should().Be("row");
            await storage.UpdateAsync(zeroRowId, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([1f, 0f, 0f]) }]));
            var definition = await engine.CreateSpecializedCatalogIndexAsync("vectors_cosine", table.Id, sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.Last), sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateVectorCatalogSpecializedIndexOptions(SqlVectorDistanceMetric.Cosine));
            var index = await engine.OpenIndexAsync(definition.Id);
            await ((Func<Task>)(async () => await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(100), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([0f, 0f, 0f])])))).Should().ThrowAsync<TableMutationException>();
            await ((Func<Task>)(async () => await index.SearchNearestAsync(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([0f, 0f, 0f]), 1))).Should().ThrowAsync<ArgumentException>().WithParameterName("query");
            (await index.SearchNearestAsync(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([1f, 0f, 0f]), 3)).Should().HaveCount(3);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}