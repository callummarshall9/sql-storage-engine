using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class VariableRowCodecTests
{
    [Test]
    public void EmptyAndMultibyteTextAndBinary_RoundTripWithoutConversion()
    {
        var schema = Schema();
        var expected = new Row(new SqlValue[]
        {
            SqlValue.Integer(7), SqlValue.Text(string.Empty), SqlValue.Binary(new byte[] { 0, 0xff, 0x80 }), SqlValue.Text("héllø 🌍")
        });

        var actual = RowCodec.Decode(RowCodec.Encode(expected, schema), schema);

        actual.Values.Should().Equal(expected.Values);
    }

    [Test]
    public void DecodeSelected_ReturnsOnlyRequestedFixedAndVariableColumns()
    {
        var schema = Schema();
        var encoded = RowCodec.Encode(new Row(new SqlValue[]
        {
            SqlValue.Integer(9), SqlValue.Text("skip"), SqlValue.Binary(new byte[] { 4, 5 }), SqlValue.Null
        }), schema);

        var selected = RowCodec.DecodeSelected(encoded, schema, new[] { new ColumnId(1), new ColumnId(3), new ColumnId(4) });

        selected.Keys.Should().BeEquivalentTo(new[] { new ColumnId(1), new ColumnId(3), new ColumnId(4) });
        selected[new ColumnId(1)].Should().Be(SqlValue.Integer(9));
        selected[new ColumnId(3)].Should().Be(SqlValue.Binary(new byte[] { 4, 5 }));
        selected[new ColumnId(4)].Should().Be(SqlValue.Null);
    }

    [Test]
    public void Decode_RejectsOverlappingDecreasingAndOutOfRangeVariableEntries()
    {
        var schema = Schema();
        var original = RowCodec.Encode(new Row(new SqlValue[]
        {
            SqlValue.Integer(1), SqlValue.Text("abc"), SqlValue.Binary(new byte[] { 1, 2 }), SqlValue.Text("z")
        }), schema);
        var tableOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(12)));

        var overlapping = original.ToArray();
        var firstOffset = BinaryPrimitives.ReadUInt32LittleEndian(overlapping.AsSpan(tableOffset + 4));
        BinaryPrimitives.WriteUInt32LittleEndian(overlapping.AsSpan(tableOffset + RowCodec.VariableEntryLength + 4), firstOffset + 1);
        ((Func<Row>)(() => RowCodec.Decode(overlapping, schema))).Should().Throw<StorageFormatException>();

        var decreasing = original.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(decreasing.AsSpan(tableOffset + RowCodec.VariableEntryLength + 4), firstOffset - 1);
        ((Func<Row>)(() => RowCodec.Decode(decreasing, schema))).Should().Throw<StorageFormatException>();

        var outside = original.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(outside.AsSpan(tableOffset + 8), uint.MaxValue);
        ((Func<Row>)(() => RowCodec.Decode(outside, schema))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void Encode_EnforcesMaximumInlineValueLength()
    {
        var schema = new TableDefinition(new[] { new ColumnDefinition(new ColumnId(1), "bytes", SqlType.Binary, false) });
        var oversized = new byte[RowCodec.MaximumInlineValueLength + 1];
        ((Func<byte[]>)(() => RowCodec.Encode(new Row(new[] { SqlValue.Binary(oversized) }), schema)))
            .Should().Throw<ArgumentException>();
    }

    private static TableDefinition Schema() => new(new[]
    {
        new ColumnDefinition(new ColumnId(1), "id", SqlType.Integer, false),
        new ColumnDefinition(new ColumnId(2), "name", SqlType.Text, false),
        new ColumnDefinition(new ColumnId(3), "bytes", SqlType.Binary, false),
        new ColumnDefinition(new ColumnId(4), "note", SqlType.Text, true)
    });
}
