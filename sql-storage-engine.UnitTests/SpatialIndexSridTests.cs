using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;

namespace sql_storage_engine.UnitTests;

public sealed class SpatialIndexSridTests
{
    [Test]
    public void SpatialOptionsDistinguishAllAndExactSridPolicies()
    {
        CatalogSpecializedIndexOptions.Spatial().SpatialSrid.Should().BeNull();
        CatalogSpecializedIndexOptions.Spatial(4326).SpatialSrid.Should().Be(4326);
    }

    [Test]
    public async Task ExactSridIndexRejectsIncompatibleRowsAndQueriesAndPersistsCapability()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatial-srid-{Guid.NewGuid():N}.db");
        try
        {
            RowId matchingRowId;
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var table = await engine.CreateTableAsync("places", [
                    new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
                    new CatalogColumn(new ColumnId(2), "point", SqlType.Geometry, true)
                ]);
                var storage = await engine.OpenTableAsync(table.Id);
                var otherRowId = await storage.InsertAsync(new Row([
                    SqlValue.Integer(1), SqlValue.Geometry("POINT (0 0)", 0)
                ]));
                matchingRowId = await storage.InsertAsync(new Row([
                    SqlValue.Integer(2), SqlValue.Geometry("POINT (1 1)", 4326)
                ]));
                await storage.InsertAsync(new Row([SqlValue.Integer(3), SqlValue.Null]));
                await engine.CreateIndexAsync("places_pk", table.Id, true,
                    [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)],
                    new CatalogBTreeIndexOptions(CatalogIndexStorageKind.Clustered, isPrimaryKey: true));
                var buildFailure = await ((Func<Task>)(async () =>
                        await engine.CreateSpecializedIndexAsync("places_spatial_4326", table.Id,
                            new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.First),
                            CatalogSpecializedIndexOptions.Spatial(4326))))
                    .Should().ThrowAsync<IndexBuildException>();
                buildFailure.Which.InnerException.Should().BeOfType<ArgumentException>()
                    .Which.ParamName.Should().Be("row");
                await storage.UpdateAsync(otherRowId, new RowUpdate([
                    new ColumnUpdate(1, SqlValue.Geometry("POINT (2 2)", 4326))
                ]));
                var definition = await engine.CreateSpecializedIndexAsync("places_spatial_4326", table.Id,
                    new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.First),
                    CatalogSpecializedIndexOptions.Spatial(4326));
                var index = await engine.OpenIndexAsync(definition.Id);

                definition.SpecializedOptions!.SpatialSrid.Should().Be(4326);
                (await index.SearchNearestAsync(SqlValue.Geometry("POINT (0 0)", 4326), 5))
                    .Select(match => match.RowId).Should().Equal(matchingRowId, otherRowId);
                await ((Func<Task>)(async () =>
                        await index.SearchNearestAsync(SqlValue.Geometry("POINT (0 0)", 0), 1)))
                    .Should().ThrowAsync<ArgumentException>()
                    .WithParameterName("query");

                var insertionFailure = await ((Func<Task>)(async () => await storage.InsertAsync(new Row([
                        SqlValue.Integer(4), SqlValue.Geometry("POINT (3 3)", 0)
                    ]))))
                    .Should().ThrowAsync<TableMutationException>();
                insertionFailure.Which.InnerException.Should().BeOfType<ArgumentException>()
                    .Which.ParamName.Should().Be("row");
            }

            await using var reopened = await StorageEngine.OpenAsync(path);
            var persisted = reopened.Catalog.Indexes.Single(index => index.Method == CatalogIndexMethod.Spatial);
            persisted.SpecializedOptions!.SpatialSrid.Should().Be(4326);
            var reopenedIndex = await reopened.OpenIndexAsync(persisted.Id);
            (await reopenedIndex.SearchNearestAsync(SqlValue.Geometry("POINT (0 0)", 4326), 5))
                .Should().HaveCount(2);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
