using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;

namespace sql_storage_engine.UnitTests;

public sealed class VectorIndexDistanceTests
{
    [Test]
    public async Task CosineIndexRejectsZeroBuildMutationAndQueryAndRemainsReusable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vector-distance-{Guid.NewGuid():N}.db");
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var table = await engine.CreateTableAsync("vectors", [
                new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
                new CatalogColumn(new ColumnId(2), "embedding", SqlType.Vector(3), true)
            ]);
            await engine.CreateIndexAsync(
                "vectors_pk",
                table.Id,
                true,
                [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)],
                new CatalogBTreeIndexOptions(CatalogIndexStorageKind.Clustered, isPrimaryKey: true));
            var storage = await engine.OpenTableAsync(table.Id);
            var zeroRowId = await storage.InsertAsync(new Row([
                SqlValue.Integer(0), SqlValue.Vector([0f, 0f, 0f])
            ]));
            for (var id = 1; id < 100; id++)
            {
                await storage.InsertAsync(new Row([
                    SqlValue.Integer(id), SqlValue.Vector([(float)id, 1f, -1f])
                ]));
            }

            var buildFailure = await ((Func<Task>)(async () =>
                    await engine.CreateSpecializedIndexAsync(
                        "vectors_cosine",
                        table.Id,
                        new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.Last),
                        CatalogSpecializedIndexOptions.Vector(SqlVectorDistanceMetric.Cosine))))
                .Should().ThrowAsync<IndexBuildException>();
            buildFailure.Which.InnerException.Should().BeOfType<ArgumentException>()
                .Which.ParamName.Should().Be("row");

            await storage.UpdateAsync(zeroRowId, new RowUpdate([
                new ColumnUpdate(1, SqlValue.Vector([1f, 0f, 0f]))
            ]));
            var definition = await engine.CreateSpecializedIndexAsync(
                "vectors_cosine",
                table.Id,
                new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.Last),
                CatalogSpecializedIndexOptions.Vector(SqlVectorDistanceMetric.Cosine));
            var index = await engine.OpenIndexAsync(definition.Id);

            await ((Func<Task>)(async () => await storage.InsertAsync(new Row([
                    SqlValue.Integer(100), SqlValue.Vector([0f, 0f, 0f])
                ]))))
                .Should().ThrowAsync<TableMutationException>();
            await ((Func<Task>)(async () =>
                    await index.SearchNearestAsync(SqlValue.Vector([0f, 0f, 0f]), 1)))
                .Should().ThrowAsync<ArgumentException>()
                .WithParameterName("query");

            (await index.SearchNearestAsync(SqlValue.Vector([1f, 0f, 0f]), 3))
                .Should().HaveCount(3);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
