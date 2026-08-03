using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Indexes;
using sql_storage_engine.Rows;
using sql_storage_engine.Tables;

namespace sql_storage_engine.UnitTests;

public sealed class TableStorageUpdateTests
{
    [Test]
    public async Task InPlacePartialUpdate_PreservesUnchangedColumnsAndTouchesOnlyAffectedIndex()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var id = await fixture.Table.InsertAsync(new Row([SqlValue.Integer(7), SqlValue.Text("old")]));
        var keyIndexCounts = (fixture.Indexes[0].AddCount, fixture.Indexes[0].RemoveCount);
        var valueIndexCounts = (fixture.Indexes[1].AddCount, fixture.Indexes[1].RemoveCount);

        var result = await fixture.Table.UpdateAsync(id,
            new RowUpdate([new ColumnUpdate(1, SqlValue.Text("new"))]));

        result.Should().Be(new TableUpdateResult(true, id, id));
        var row = (await fixture.Table.TryGetAsync(id)).Row!;
        row.Values[0].Should().Be(SqlValue.Integer(7));
        row.Values[1].Should().Be(SqlValue.Text("new"));
        (fixture.Indexes[0].AddCount, fixture.Indexes[0].RemoveCount).Should().Be(keyIndexCounts);
        (fixture.Indexes[1].AddCount, fixture.Indexes[1].RemoveCount).Should()
            .Be((valueIndexCounts.Item1 + 1, valueIndexCounts.Item2 + 1));
    }

    [Test]
    public async Task Relocation_UpdatesEveryIndexRowIdAndMakesOldIdInaccessible()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync(inlineThreshold: 6000);
        var original = new Row([SqlValue.Integer(7), SqlValue.Text("old")]);
        var id = await fixture.Table.InsertAsync(original);
        await fixture.Table.InsertAsync(new Row([SqlValue.Integer(8), SqlValue.Text(new string('f', 5000))]));

        var result = await fixture.Table.UpdateAsync(id,
            new RowUpdate([new ColumnUpdate(1, SqlValue.Text(new string('n', 4000)))]));

        result.Relocated.Should().BeTrue();
        (await fixture.Table.TryGetAsync(id)).Found.Should().BeFalse();
        (await fixture.Table.TryGetAsync(result.CurrentRowId)).Found.Should().BeTrue();
        var updated = new Row([SqlValue.Integer(7), SqlValue.Text(new string('n', 4000))]);
        foreach (var index in fixture.Indexes)
            (await index.Tree.FindAsync(CatalogIndexKey.Encode(updated, fixture.Definition, index.Definition)))
                .Should().Equal(result.CurrentRowId);
    }

    [Test]
    public async Task UniqueKeyFailure_RestoresPreviousLogicalHeapAndIndexState()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync(uniqueFirst: true);
        var first = new Row([SqlValue.Integer(1), SqlValue.Text("one")]);
        var second = new Row([SqlValue.Integer(2), SqlValue.Text("two")]);
        var firstId = await fixture.Table.InsertAsync(first);
        await fixture.Table.InsertAsync(second);

        var assertion = await ((Func<Task>)(async () => await fixture.Table.UpdateAsync(firstId,
            new RowUpdate([new ColumnUpdate(0, SqlValue.Integer(2))])))).Should()
            .ThrowAsync<TableMutationException>();

        assertion.Which.UnreclaimedPageIds.Should().BeEmpty();
        (await fixture.Table.TryGetAsync(firstId)).Row!.Values.Should().BeEquivalentTo(first.Values);
        (await fixture.Indexes[0].Tree.FindAsync(CatalogIndexKey.Encode(first, fixture.Definition,
            fixture.Indexes[0].Definition))).Should().Equal(firstId);
    }
}
