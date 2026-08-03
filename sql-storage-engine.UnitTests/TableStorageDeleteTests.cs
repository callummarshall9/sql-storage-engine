using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Buffers;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Overflow;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;

namespace sql_storage_engine.UnitTests;

public sealed class TableStorageDeleteTests
{
    [Test]
    public async Task Delete_MakesHeapRowInaccessibleRemovesEveryIndexAndReclaimsOverflow()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var row = new Row([SqlValue.Integer(7), SqlValue.Text(new string('x', 100))]);
        var id = await fixture.Table.InsertAsync(row);
        var encoded = await fixture.Heap.ReadAsync(id);
        var schema = new TableDefinition(fixture.Definition.Columns.Select(column =>
            new ColumnDefinition(column.Id, column.Name, column.Type, column.IsNullable)));
        var overflow = new OverflowRowCodec(new sql_storage_engine.Overflow.OverflowManager(fixture.Pool, fixture.Pages), 16)
            .GetOverflowReferences(encoded.Row.Span, schema).Values.Single();

        var result = await fixture.Table.DeleteAsync(id);

        result.Should().BeEquivalentTo(new TableDeleteResult(true, Array.Empty<sql_storage_engine.Identifiers.PageId>()));
        (await fixture.Table.TryGetAsync(id)).Found.Should().BeFalse();
        foreach (var index in fixture.Indexes)
            (await index.Tree.FindAsync(CatalogIndexKey.Encode(row, fixture.Definition, index.Definition))).Should().BeEmpty();
        await ((Func<Task>)(async () => await fixture.Pages.ReadAsync(overflow.FirstPageId,
            new byte[fixture.Pages.PageSize]))).Should().ThrowAsync<StorageResourceException>();
    }

    [Test]
    public async Task MissingRow_ReturnsFalseWithoutMutation()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var row = new Row([SqlValue.Integer(7), SqlValue.Text("value")]);
        var id = await fixture.Table.InsertAsync(row);
        var counts = fixture.Indexes.Select(index => (index.AddCount, index.RemoveCount)).ToArray();

        var result = await fixture.Table.DeleteAsync(id with { Generation = new sql_storage_engine.Identifiers.SlotGeneration(99) });

        result.Deleted.Should().BeFalse();
        fixture.Indexes.Select(index => (index.AddCount, index.RemoveCount)).Should().Equal(counts);
        (await fixture.Table.TryGetAsync(id)).Found.Should().BeTrue();
    }

    [Test]
    public async Task MissingIndexEntry_FailsAndRestoresPreviouslyRemovedEntries()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var row = new Row([SqlValue.Integer(7), SqlValue.Text("value")]);
        var id = await fixture.Table.InsertAsync(row);
        var secondKey = CatalogIndexKey.Encode(row, fixture.Definition, fixture.Indexes[1].Definition);
        await fixture.Indexes[1].Tree.RemoveAsync(secondKey, id);

        var assertion = await ((Func<Task>)(async () => await fixture.Table.DeleteAsync(id))).Should()
            .ThrowAsync<TableMutationException>();

        assertion.Which.UnreclaimedPageIds.Should().BeEmpty();
        (await fixture.Table.TryGetAsync(id)).Found.Should().BeTrue();
        var firstKey = CatalogIndexKey.Encode(row, fixture.Definition, fixture.Indexes[0].Definition);
        (await fixture.Indexes[0].Tree.FindAsync(firstKey)).Should().Equal(id);
    }

    [Test]
    public async Task OverflowReclamationFailure_IsReportedForDeferredCleanup()
    {
        await using var inner = new InMemoryPageStore();
        await using var faulting = new FaultInjectingPageStore(inner, inner);
        await using var pool = new BufferPool(faulting, 8, leaveOpen: true);
        var heap = await TableHeap.CreateAsync(pool, faulting);
        var definition = new CatalogTable(new TableId(1), "items", 1, heap.RootPageId,
            [new CatalogColumn(new ColumnId(1), "value", SqlType.Text, false)]);
        var overflow = new OverflowManager(pool, faulting);
        var codec = new OverflowRowCodec(overflow, 8);
        var table = new TableStorage(definition, heap, codec, overflow, []);
        var id = await table.InsertAsync(new Row([SqlValue.Text(new string('x', 100))]));
        var encoded = await heap.ReadAsync(id);
        var reference = codec.GetOverflowReferences(encoded.Row.Span,
            new TableDefinition([new ColumnDefinition(new ColumnId(1), "value", SqlType.Text, false)])).Values.Single();
        faulting.FailOn = FaultInjectingPageStore.Operation.Free;

        var result = await table.DeleteAsync(id);

        result.Deleted.Should().BeTrue();
        result.DeferredCleanupPageIds.Should().Equal(reference.FirstPageId);
        (await table.TryGetAsync(id)).Found.Should().BeFalse();
        faulting.FailOn = FaultInjectingPageStore.Operation.None;
    }
}
