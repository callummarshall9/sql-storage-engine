using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class CatalogModelTests
{
    [Test]
    public void Models_PreserveTypedIdentityNullabilityAndCompositeSortConfiguration()
    {
        var table = Table(new CatalogColumn(new ColumnId(1), "first", SqlType.Text, true),
            new CatalogColumn(new ColumnId(2), "second", SqlType.Integer, false));
        var index = new CatalogIndex(new IndexId(9), "by_values", table.Id, new PageId(22), true,
        [
            new CatalogIndexedColumn(new ColumnId(1), SortDirection.Descending, NullSortOrder.Last, "ordinal"),
            new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.First)
        ]);

        var catalog = new CatalogDefinition([table], [index]);

        catalog.Tables[0].Columns[0].IsNullable.Should().BeTrue();
        catalog.Indexes[0].Columns.Select(column => column.ColumnId).Should()
            .Equal(new ColumnId(1), new ColumnId(2));
        catalog.Indexes[0].Columns[0].Should().BeEquivalentTo(
            new CatalogIndexedColumn(new ColumnId(1), SortDirection.Descending, NullSortOrder.Last, "ordinal"));
    }

    [Test]
    public void Names_AreMutableMetadataIndependentOfStablePhysicalIdentifiers()
    {
        var original = Table(new CatalogColumn(new ColumnId(1), "value", SqlType.Integer, false));
        var renamed = new CatalogTable(original.Id, "renamed", original.SchemaVersion + 1,
            original.FirstHeapPageId, original.Columns);

        renamed.Id.Should().Be(original.Id);
        renamed.FirstHeapPageId.Should().Be(original.FirstHeapPageId);
        renamed.Name.Should().NotBe(original.Name);
    }

    [TestCaseSource(nameof(InvalidCatalogs))]
    public void Validation_RejectsDuplicateIdentifiersNamesAndInvalidReferences(Func<CatalogDefinition> create)
    {
        create.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Collections_AreSnapshotsAndCannotBeMutatedThroughPublicApi()
    {
        var source = new List<CatalogColumn> { new(new ColumnId(1), "value", SqlType.Integer, false) };
        var table = new CatalogTable(new TableId(1), "items", 1, new PageId(4), source);
        source.Add(new CatalogColumn(new ColumnId(2), "later", SqlType.Text, true));

        table.Columns.Should().HaveCount(1);
        table.Columns.Should().BeAssignableTo<IReadOnlyList<CatalogColumn>>();
    }

    private static IEnumerable<TestCaseData> InvalidCatalogs()
    {
        var table = Table(new CatalogColumn(new ColumnId(1), "value", SqlType.Integer, false));
        yield return new TestCaseData(() => new CatalogDefinition([table,
            new CatalogTable(table.Id, "other", table.SchemaVersion, table.FirstHeapPageId, table.Columns)], []))
            .SetName("Duplicate table ID");
        yield return new TestCaseData(() => new CatalogDefinition([table,
            new CatalogTable(new TableId(2), table.Name, table.SchemaVersion, table.FirstHeapPageId, table.Columns)], []))
            .SetName("Duplicate table name");
        yield return new TestCaseData(() => new CatalogDefinition([table],
            [Index(new IndexId(1), "same", table.Id, new ColumnId(1)),
             Index(new IndexId(2), "same", table.Id, new ColumnId(1))])).SetName("Duplicate index name in table");
        yield return new TestCaseData(() => new CatalogDefinition([table],
            [Index(new IndexId(1), "index", new TableId(99), new ColumnId(1))])).SetName("Unknown table");
        yield return new TestCaseData(() => new CatalogDefinition([table],
            [Index(new IndexId(1), "index", table.Id, new ColumnId(99))])).SetName("Unknown column");
        yield return new TestCaseData((Func<CatalogDefinition>)(() => new CatalogDefinition([Table(
            new CatalogColumn(new ColumnId(1), "one", SqlType.Integer, false),
            new CatalogColumn(new ColumnId(1), "two", SqlType.Integer, false))], []))).SetName("Duplicate column ID");
        yield return new TestCaseData((Func<CatalogDefinition>)(() => new CatalogDefinition([Table(
            new CatalogColumn(new ColumnId(1), "same", SqlType.Integer, false),
            new CatalogColumn(new ColumnId(2), "same", SqlType.Integer, false))], []))).SetName("Duplicate column name");
    }

    private static CatalogTable Table(params CatalogColumn[] columns) =>
        new(new TableId(1), "items", 3, new PageId(8), columns);

    private static CatalogIndex Index(IndexId id, string name, TableId tableId, ColumnId columnId) =>
        new(id, name, tableId, new PageId(9), false,
            [new CatalogIndexedColumn(columnId, SortDirection.Ascending, NullSortOrder.Last)]);
}
