// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class OverflowRowCodecTests
{
    [Test]
    public async Task ValuesBelowAndAboveThresholdUseExpectedStorageAndDecodeIdentically()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 4, leaveOpen: true);
        var codec = new sql_storage_engine.Exposures.Rows.OverflowRowCodecExposure(new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(pool, store), 16);
        var schema = Schema();
        var expected = RowExposure.CreateRow(new SqlValue[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("small"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(Enumerable.Range(0, 100).Select(value => (byte)value).ToArray()) });
        var encoded = await codec.EncodeAsync(expected,schema);
        var decoded = await codec.DecodeAsync(encoded.Bytes,schema);
        codec.GetStorage(encoded.Bytes,schema,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }).Should().Be(RowValueStorage.Inline);
        codec.GetStorage(encoded.Bytes,schema,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }).Should().Be(RowValueStorage.Overflow);
        encoded.NewlyAllocated.Should().ContainSingle();
        decoded.Values.Should().Equal(expected.Values);
        var boundary = await codec.EncodeAsync(RowExposure.CreateRow(new[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('x', 8)), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 1 }) }),schema);
        codec.GetStorage(boundary.Bytes,schema,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }).Should().Be(RowValueStorage.Inline);
    }

    [Test]
    public async Task GrowingAndShrinkingAcrossThresholdReportsNewAndRetiredChains()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 4, leaveOpen: true);
        var manager = new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(pool, store);
        var codec = new sql_storage_engine.Exposures.Rows.OverflowRowCodecExposure(manager, 8);
        var schema = Schema();
        var initial = await codec.EncodeAsync(RowExposure.CreateRow(new[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("tiny"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 1 }) }),schema);
        var grown = await codec.ApplyUpdateAsync(initial.Bytes,RowUpdateExposure.CreateRowUpdate(new[] { new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('x', 100)) } }),schema);
        grown.NewlyAllocated.Should().ContainSingle();
        grown.Retired.Should().BeEmpty();
        codec.GetStorage(grown.Bytes,schema,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }).Should().Be(RowValueStorage.Overflow);
        var oldReference = grown.NewlyAllocated.Single();
        var shrunk = await codec.ApplyUpdateAsync(grown.Bytes,RowUpdateExposure.CreateRowUpdate(new[] { new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("ok") } }),schema);
        shrunk.NewlyAllocated.Should().BeEmpty();
        shrunk.Retired.Should().Equal(oldReference);
        codec.GetStorage(shrunk.Bytes,schema,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }).Should().Be(RowValueStorage.Inline);
        (await codec.DecodeAsync(shrunk.Bytes,schema)).Values.Should().Equal(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("ok"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 1 }));
    }

    [Test]
    public async Task FailedUpdateLeavesOldReferenceReadableAndInputUnchanged()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 4, leaveOpen: true);
        var manager = new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(pool, store);
        var codec = new sql_storage_engine.Exposures.Rows.OverflowRowCodecExposure(manager, 4);
        var schema = Schema();
        var encoded = await codec.EncodeAsync(RowExposure.CreateRow(new[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("long old value"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 1 }) }),schema);
        var original = encoded.Bytes.ToArray();
        var oldReference = codec.GetOverflowReferences(encoded.Bytes,schema)[new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }];
        await ((Func<Task>)(async () => await codec.ApplyUpdateAsync(encoded.Bytes,RowUpdateExposure.CreateRowUpdate(new[] { new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 9 }) } }),schema))).Should().ThrowAsync<ArgumentException>();
        encoded.Bytes.Should().Equal(original);
        (await manager.ReadAsync(oldReference)).Length.Should().BeGreaterThan(0);
        (await codec.DecodeAsync(encoded.Bytes,schema)).Values[0].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("long old value"));
    }

    private static TableDefinition Schema() => sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition(new[] { sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"text",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharMaxSqlType(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"bytes",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeVarBinaryMax(),false) });
}