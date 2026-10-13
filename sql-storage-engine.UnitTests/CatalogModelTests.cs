using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

using sql_storage_engine.Models.Rows;
namespace sql_storage_engine.UnitTests;
public sealed class CatalogModelTests
{
    [Test]
    public void Models_PreserveTypedIdentityNullabilityAndCompositeSortConfiguration()
    {
        var table = Table(sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "first", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(100), true),sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "second", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false));
        var index = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 9 },name: "by_values",tableId: table.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 22 },isUnique: true,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Descending,NullSortOrder.Last,"ordinal"), sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.First)]);
        var catalog = sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([table], [index]);
        catalog.Tables[0].Columns[0].IsNullable.Should().BeTrue();
        catalog.Indexes[0].Columns.Select(column => column.ColumnId).Should().Equal(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 });
        catalog.Indexes[0].Columns[0].Should().BeEquivalentTo(sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Descending,NullSortOrder.Last,"ordinal"));
    }

    [Test]
    public void Names_AreMutableMetadataIndependentOfStablePhysicalIdentifiers()
    {
        var original = Table(sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false));
        var renamed = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(original.Id, "renamed", original.SchemaVersion + 1, original.FirstHeapPageId, original.Columns);
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
        var source = new List<CatalogColumn>
        {
            sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false)
        };
        var table = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "items", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 4 }, source);
        source.Add(sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "later", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(4000), true));
        table.Columns.Should().HaveCount(1);
        table.Columns.Should().BeAssignableTo<IReadOnlyList<CatalogColumn>>();
    }

    private static IEnumerable<TestCaseData> InvalidCatalogs()
    {
        var table = Table(sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false));
        yield return new TestCaseData(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([table, sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(table.Id, "other", table.SchemaVersion, table.FirstHeapPageId, table.Columns)], [])).SetName("Duplicate table ID");
        yield return new TestCaseData(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([table, sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 2 }, table.Name, table.SchemaVersion, table.FirstHeapPageId, table.Columns)], [])).SetName("Duplicate table name");
        yield return new TestCaseData(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([table], [Index(new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 },"same",table.Id,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }), Index(new sql_storage_engine.Models.Identifiers.IndexId { Value = 2 },"same",table.Id,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 })])).SetName("Duplicate index name in table");
        yield return new TestCaseData(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([table], [Index(new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 },"index",new sql_storage_engine.Models.Identifiers.TableId { Value = 99 },new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 })])).SetName("Unknown table");
        yield return new TestCaseData(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([table], [Index(new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 },"index",table.Id,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 99 })])).SetName("Unknown column");
        yield return new TestCaseData((Func<CatalogDefinition>)(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([Table(sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "one", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false),sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "two", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false))], []))).SetName("Duplicate column ID");
        yield return new TestCaseData((Func<CatalogDefinition>)(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([Table(sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "same", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false),sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "same", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false))], []))).SetName("Duplicate column name");
    }

    private static CatalogTable Table(params CatalogColumn[] columns) => sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "items", 3, new sql_storage_engine.Models.Identifiers.PageId { Value = 8 }, columns);
    private static CatalogIndex Index(IndexId id, string name, TableId tableId, ColumnId columnId) => sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: id,name: name,tableId: tableId,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 9 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(columnId,SortDirection.Ascending,NullSortOrder.Last)]);
}