// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Buffers;
using sql_storage_engine.Heap;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Indexes;

using sql_storage_engine.Models.Catalog;
namespace sql_storage_engine.UnitTests;
public sealed class TableStorageDeleteTests
{
    [Test]
    public async Task Delete_MakesHeapRowInaccessibleRemovesEveryIndexAndReclaimsOverflow()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var row = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('x', 100))]);
        var id = await fixture.Table.InsertAsync(row);
        var encoded = await fixture.Heap.ReadAsync(id);
        var schema = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition(fixture.Definition.Columns.Select(column => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(column.Id, column.Name, column.Type, column.IsNullable)));
        var overflow = new sql_storage_engine.Exposures.Rows.OverflowRowCodecExposure(new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(fixture.Pool, fixture.Pages), 16).GetOverflowReferences(encoded.Row.Span, schema).Values.Single();
        var result = await fixture.Table.DeleteAsync(id);
        result.Should().BeEquivalentTo(new TableDeleteResult { Deleted = true, DeferredCleanupPageIds = Array.Empty<sql_storage_engine.Models.Identifiers.PageId>() });
        (await fixture.Table.TryGetAsync(id)).Found.Should().BeFalse();
        foreach (var index in fixture.Indexes)
            (await index.ReadTree().FindAsync(sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.Encode(row, fixture.Definition, index.ReadDefinition()))).Should().BeEmpty();
        await ((Func<Task>)(async () => await fixture.Pages.ReadAsync(overflow.FirstPageId, new byte[fixture.Pages.GetPageSize()]))).Should().ThrowAsync<StorageResourceException>();
    }

    [Test]
    public async Task MissingRow_ReturnsFalseWithoutMutation()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var row = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("value")]);
        var id = await fixture.Table.InsertAsync(row);
        var counts = fixture.Indexes.Select(index => (index.ReadAddCount(), index.ReadRemoveCount())).ToArray();
        var result = await fixture.Table.DeleteAsync(id with { Generation = new sql_storage_engine.Models.Identifiers.SlotGeneration { Value = 99 } });
        result.Deleted.Should().BeFalse();
        fixture.Indexes.Select(index => (index.ReadAddCount(), index.ReadRemoveCount())).Should().Equal(counts);
        (await fixture.Table.TryGetAsync(id)).Found.Should().BeTrue();
    }

    [Test]
    public async Task MissingIndexEntry_FailsAndRestoresPreviouslyRemovedEntries()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var row = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("value")]);
        var id = await fixture.Table.InsertAsync(row);
        sql_storage_engine.Models.Indexes.IndexKey secondKey = sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.Encode(row, fixture.Definition, fixture.Indexes[1].ReadDefinition());
        await fixture.Indexes[1].ReadTree().RemoveAsync(secondKey, id);
        var assertion = await ((Func<Task>)(async () => await fixture.Table.DeleteAsync(id))).Should().ThrowAsync<TableMutationException>();
        assertion.Which.UnreclaimedPageIds.Should().BeEmpty();
        (await fixture.Table.TryGetAsync(id)).Found.Should().BeTrue();
        sql_storage_engine.Models.Indexes.IndexKey firstKey = sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.Encode(row, fixture.Definition, fixture.Indexes[0].ReadDefinition());
        (await fixture.Indexes[0].ReadTree().FindAsync(firstKey)).Should().Equal(id);
    }

    [Test]
    public async Task OverflowReclamationFailure_IsReportedForDeferredCleanup()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure inner = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using var faulting = new FaultInjectingPageStore(inner, inner);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(faulting, 8, leaveOpen: true);
        var heap = await sql_storage_engine.Exposures.Heap.TableHeapExposure.CreateTableHeapExposureAsync(pool,faulting);
        var definition = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "items", 1, heap.ReadRootPageId(), [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(4000), false)]);
        var overflow = new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(pool, faulting);
        var codec = new sql_storage_engine.Exposures.Rows.OverflowRowCodecExposure(overflow, 8);
        var table = new sql_storage_engine.Exposures.Tables.TableStorageExposure(catalogTable:definition,heap:heap,rowCodec:codec,indexes:[]);
        var id = await table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('x', 100))]));
        var encoded = await heap.ReadAsync(id);
        var reference = codec.GetOverflowReferences(encoded.Row.Span,sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition([sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"value",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(4000),false)])).Values.Single();
        faulting.FailOn = FaultInjectingPageStore.Operation.Free;
        var result = await table.DeleteAsync(id);
        result.Deleted.Should().BeTrue();
        result.DeferredCleanupPageIds.Should().Equal(reference.FirstPageId);
        (await table.TryGetAsync(id)).Found.Should().BeFalse();
        faulting.FailOn = FaultInjectingPageStore.Operation.None;
    }
}