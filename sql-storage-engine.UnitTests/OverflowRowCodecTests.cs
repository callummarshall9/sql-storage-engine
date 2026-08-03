using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Overflow;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class OverflowRowCodecTests
{
    [Test]
    public async Task ValuesBelowAndAboveThresholdUseExpectedStorageAndDecodeIdentically()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 4, leaveOpen: true);
        var codec = new OverflowRowCodec(new OverflowManager(pool, store), 16);
        var schema = Schema();
        var expected = new Row(new SqlValue[]
        {
            SqlValue.Text("small"), SqlValue.Binary(Enumerable.Range(0, 100).Select(value => (byte)value).ToArray())
        });

        var encoded = await codec.EncodeAsync(expected, schema);
        var decoded = await codec.DecodeAsync(encoded.Bytes, schema);

        codec.GetStorage(encoded.Bytes, schema, new ColumnId(1)).Should().Be(RowValueStorage.Inline);
        codec.GetStorage(encoded.Bytes, schema, new ColumnId(2)).Should().Be(RowValueStorage.Overflow);
        encoded.NewlyAllocated.Should().ContainSingle();
        decoded.Values.Should().Equal(expected.Values);

        var boundary = await codec.EncodeAsync(new Row(new[]
        {
            SqlValue.Text(new string('x', 16)), SqlValue.Binary(new byte[] { 1 })
        }), schema);
        codec.GetStorage(boundary.Bytes, schema, new ColumnId(1)).Should().Be(RowValueStorage.Inline);
    }

    [Test]
    public async Task GrowingAndShrinkingAcrossThresholdReportsNewAndRetiredChains()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 4, leaveOpen: true);
        var manager = new OverflowManager(pool, store);
        var codec = new OverflowRowCodec(manager, 8);
        var schema = Schema();
        var initial = await codec.EncodeAsync(new Row(new[] { SqlValue.Text("tiny"), SqlValue.Binary(new byte[] { 1 }) }), schema);

        var grown = await codec.ApplyUpdateAsync(initial.Bytes,
            new RowUpdate(new[] { new ColumnUpdate(0, SqlValue.Text(new string('x', 100))) }), schema);

        grown.NewlyAllocated.Should().ContainSingle();
        grown.Retired.Should().BeEmpty();
        codec.GetStorage(grown.Bytes, schema, new ColumnId(1)).Should().Be(RowValueStorage.Overflow);
        var oldReference = grown.NewlyAllocated.Single();

        var shrunk = await codec.ApplyUpdateAsync(grown.Bytes,
            new RowUpdate(new[] { new ColumnUpdate(0, SqlValue.Text("ok")) }), schema);

        shrunk.NewlyAllocated.Should().BeEmpty();
        shrunk.Retired.Should().Equal(oldReference);
        codec.GetStorage(shrunk.Bytes, schema, new ColumnId(1)).Should().Be(RowValueStorage.Inline);
        (await codec.DecodeAsync(shrunk.Bytes, schema)).Values.Should().Equal(SqlValue.Text("ok"), SqlValue.Binary(new byte[] { 1 }));
    }

    [Test]
    public async Task FailedUpdateLeavesOldReferenceReadableAndInputUnchanged()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 4, leaveOpen: true);
        var manager = new OverflowManager(pool, store);
        var codec = new OverflowRowCodec(manager, 4);
        var schema = Schema();
        var encoded = await codec.EncodeAsync(new Row(new[] { SqlValue.Text("long old value"), SqlValue.Binary(new byte[] { 1 }) }), schema);
        var original = encoded.Bytes.ToArray();
        var oldReference = codec.GetOverflowReferences(encoded.Bytes, schema)[new ColumnId(1)];

        await ((Func<Task>)(async () => await codec.ApplyUpdateAsync(encoded.Bytes,
            new RowUpdate(new[] { new ColumnUpdate(0, SqlValue.Binary(new byte[] { 9 })) }), schema)))
            .Should().ThrowAsync<ArgumentException>();

        encoded.Bytes.Should().Equal(original);
        (await manager.ReadAsync(oldReference)).Length.Should().BeGreaterThan(0);
        (await codec.DecodeAsync(encoded.Bytes, schema)).Values[0].Should().Be(SqlValue.Text("long old value"));
    }

    private static TableDefinition Schema() => new(new[]
    {
        new ColumnDefinition(new ColumnId(1), "text", SqlType.Text, false),
        new ColumnDefinition(new ColumnId(2), "bytes", SqlType.Binary, false)
    });
}
