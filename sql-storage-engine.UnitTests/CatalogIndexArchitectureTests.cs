using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Exposures.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class CatalogIndexArchitectureTests
{
    [Test]
    public void IndexCreationMaterializesColumnsOnceAndSnapshotsIncludes()
    {
        var column = CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.Last);
        var reads = 0;
        IEnumerable<CatalogIndexedColumn> Columns()
        {
            reads++;
            if (reads > 1) throw new InvalidOperationException("Enumerated twice");
            yield return column;
        }
        ColumnId[] included = [new ColumnId { Value = 2 }];
        var index = CatalogIndexExposure.CreateCatalogIndex(new IndexId { Value = 1 }, "ix", new TableId { Value = 1 }, new PageId { Value = 2 }, false, Columns(), includedColumns: included);
        reads.Should().Be(1);
        included[0] = new ColumnId { Value = 3 };
        index.IncludedColumns.Should().Equal(new ColumnId { Value = 2 });
        ((Action)(() => ((IList<CatalogIndexedColumn>)index.Columns)[0] = column)).Should().Throw<NotSupportedException>();
        index.Method.Should().Be(CatalogIndexMethod.BTree);
    }

    [Test]
    public void SpecializedIndexAdmissionValidatesRawPathsAndIndexConstraints()
    {
        var column = CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.Last);
        var options = new CatalogSpecializedIndexOptions { Method = CatalogIndexMethod.Json, JsonPaths = ["$.a", "$.a.b"] };
        ((Action)(() => CatalogIndexExposure.CreateCatalogIndex(new IndexId { Value = 1 },"ix",new TableId { Value = 1 },new PageId { Value = 2 },false,[column],options))).Should().Throw<ArgumentException>();
        options = CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions("$.a");
        ((Action)(() => CatalogIndexExposure.CreateCatalogIndex(new IndexId { Value = 1 },"ix",new TableId { Value = 1 },new PageId { Value = 2 },true,[column],options))).Should().Throw<ArgumentException>();
        var valid = CatalogIndexExposure.CreateCatalogIndex(new IndexId { Value = 1 },"ix",new TableId { Value = 1 },new PageId { Value = 2 },false,[column],options);
        valid.Method.Should().Be(CatalogIndexMethod.Json);
        valid.SpecializedOptions!.JsonPaths.Should().Equal("$.a");
    }
}