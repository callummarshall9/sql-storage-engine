using sql_storage_engine.Models.Tables;
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Indexes;
using sql_storage_engine.Rows;
using sql_storage_engine.Tables;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;

using sql_storage_engine.Models.Catalog;
namespace sql_storage_engine.UnitTests;
public sealed class TableStorageUpdateTests
{
    [Test]
    public async Task InPlacePartialUpdate_PreservesUnchangedColumnsAndTouchesOnlyAffectedIndex()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var id = await fixture.Table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("old")]));
        var keyIndexCounts = (fixture.Indexes[0].ReadAddCount(), fixture.Indexes[0].ReadRemoveCount());
        var valueIndexCounts = (fixture.Indexes[1].ReadAddCount(), fixture.Indexes[1].ReadRemoveCount());
        var result = await fixture.Table.UpdateAsync(id, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("new") }]));
        result.Should().Be(new TableUpdateResult {Updated=true,PreviousRowId=id,CurrentRowId=id,Relocated=false});
        var row = (await fixture.Table.TryGetAsync(id)).Row!;
        row.Values[0].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7));
        row.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("new"));
        (fixture.Indexes[0].ReadAddCount(), fixture.Indexes[0].ReadRemoveCount()).Should().Be(keyIndexCounts);
        (fixture.Indexes[1].ReadAddCount(), fixture.Indexes[1].ReadRemoveCount()).Should().Be((valueIndexCounts.Item1 + 1, valueIndexCounts.Item2 + 1));
    }

    [Test]
    public async Task Relocation_UpdatesEveryIndexRowIdAndMakesOldIdInaccessible()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync(inlineThreshold: 6000);
        var original = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("old")]);
        var id = await fixture.Table.InsertAsync(original);
        await fixture.Table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(8), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('f', 1600))]));
        await fixture.Table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(9), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('g', 1600))]));
        await fixture.Table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(10), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('h', 1600))]));
        await fixture.Table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(11), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('i', 1600))]));
        var result = await fixture.Table.UpdateAsync(id, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('n', 1700)) }]));
        result.Relocated.Should().BeTrue();
        (await fixture.Table.TryGetAsync(id)).Found.Should().BeFalse();
        (await fixture.Table.TryGetAsync(result.CurrentRowId)).Found.Should().BeTrue();
        var updated = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('n', 1700))]);
        foreach (var index in fixture.Indexes)
            (await index.ReadTree().FindAsync(sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.Encode(updated, fixture.Definition, index.ReadDefinition()))).Should().Equal(result.CurrentRowId);
    }

    [Test]
    public async Task UniqueKeyFailure_RestoresPreviousLogicalHeapAndIndexState()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync(uniqueFirst: true);
        var first = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("one")]);
        var second = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("two")]);
        var firstId = await fixture.Table.InsertAsync(first);
        await fixture.Table.InsertAsync(second);
        var assertion = await ((Func<Task>)(async () => await fixture.Table.UpdateAsync(firstId, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2) }])))).Should().ThrowAsync<TableMutationException>();
        assertion.Which.UnreclaimedPageIds.Should().BeEmpty();
        (await fixture.Table.TryGetAsync(firstId)).Row!.Values.Should().BeEquivalentTo(first.Values);
        (await fixture.Indexes[0].ReadTree().FindAsync(sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.Encode(first, fixture.Definition, fixture.Indexes[0].ReadDefinition()))).Should().Equal(firstId);
    }
}