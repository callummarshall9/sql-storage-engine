using AwesomeAssertions;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class FixedRowCodecTests
{
    [Test]
    public void FixedValuesAndNullCombinations_RoundTrip()
    {
        var schema = Schema();
        var rows = new[]
        {
            new Row(new[] { SqlValue.Boolean(false), SqlValue.Integer(long.MinValue), SqlValue.Null }),
            new Row(new[] { SqlValue.Boolean(true), SqlValue.Integer(long.MaxValue), SqlValue.Integer(0) })
        };

        foreach (var expected in rows)
        {
            var actual = RowCodec.Decode(RowCodec.Encode(expected, schema), schema);
            actual.Values.Should().Equal(expected.Values);
        }
    }

    [Test]
    public void BoundaryValues_HaveGoldenLittleEndianBytes()
    {
        var schema = Schema();
        var encoded = RowCodec.Encode(
            new Row(new[] { SqlValue.Boolean(true), SqlValue.Integer(long.MinValue), SqlValue.Null }), schema);

        Convert.ToHexString(encoded.AsSpan(32)).Should().Be("040100000000000000800000000000000000");
        encoded.Length.Should().Be(50);
        BitConverter.ToUInt16(encoded, 0).Should().Be(1);
        BitConverter.ToUInt16(encoded, 2).Should().Be(3);
        BitConverter.ToUInt16(encoded, 4).Should().Be(1);
    }

    [Test]
    public void Encode_RejectsColumnCountTypeAndNullabilityMismatch()
    {
        var schema = Schema();
        ((Func<byte[]>)(() => RowCodec.Encode(new Row(new[] { SqlValue.Boolean(true) }), schema)))
            .Should().Throw<ArgumentException>();
        ((Func<byte[]>)(() => RowCodec.Encode(
            new Row(new[] { SqlValue.Integer(1), SqlValue.Integer(2), SqlValue.Integer(3) }), schema)))
            .Should().Throw<ArgumentException>();
        ((Func<byte[]>)(() => RowCodec.Encode(
            new Row(new[] { SqlValue.Null, SqlValue.Integer(2), SqlValue.Integer(3) }), schema)))
            .Should().Throw<ArgumentException>();
    }

    [Test]
    public void Decode_RejectsTruncationSchemaMismatchAndUncheckedLengths()
    {
        var schema = Schema();
        var encoded = RowCodec.Encode(
            new Row(new[] { SqlValue.Boolean(true), SqlValue.Integer(1), SqlValue.Null }), schema);
        ((Func<Row>)(() => RowCodec.Decode(encoded.AsSpan(0, 31), schema))).Should().Throw<StorageFormatException>();
        ((Func<Row>)(() => RowCodec.Decode(encoded.AsSpan(0, encoded.Length - 1), schema))).Should().Throw<StorageFormatException>();
        var otherSchema = new TableDefinition(new[]
        {
            new ColumnDefinition(new ColumnId(99), "active", SqlType.Boolean, false),
            new ColumnDefinition(new ColumnId(2), "minimum", SqlType.Integer, false),
            new ColumnDefinition(new ColumnId(3), "optional", SqlType.Integer, true)
        });
        ((Func<Row>)(() => RowCodec.Decode(encoded, otherSchema))).Should().Throw<StorageFormatException>();
        encoded[20] = 0xff;
        ((Func<Row>)(() => RowCodec.Decode(encoded, schema))).Should().Throw<StorageFormatException>();
    }

    private static TableDefinition Schema() => new(new[]
    {
        new ColumnDefinition(new ColumnId(1), "active", SqlType.Boolean, false),
        new ColumnDefinition(new ColumnId(2), "minimum", SqlType.Integer, false),
        new ColumnDefinition(new ColumnId(3), "optional", SqlType.Integer, true)
    });
}
