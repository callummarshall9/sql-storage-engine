using AwesomeAssertions;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class RowUpdateTests
{
    [Test]
    public void ApplyUpdate_OneFixedOrVariableColumnPreservesEveryOtherValue()
    {
        var schema = Schema();
        var original = new Row(new SqlValue[]
        {
            SqlValue.Integer(1), SqlValue.Text("old"), SqlValue.Boolean(true), SqlValue.Binary(new byte[] { 4, 5 })
        });
        var encoded = RowCodec.Encode(original, schema);

        var fixedUpdated = RowCodec.Decode(RowCodec.ApplyUpdate(encoded,
            new RowUpdate(new[] { new ColumnUpdate(0, SqlValue.Integer(2)) }), schema), schema);
        fixedUpdated.Values.Should().Equal(
            SqlValue.Integer(2), SqlValue.Text("old"), SqlValue.Boolean(true), SqlValue.Binary(new byte[] { 4, 5 }));

        var variableUpdated = RowCodec.Decode(RowCodec.ApplyUpdate(encoded,
            new RowUpdate(new[] { new ColumnUpdate(1, SqlValue.Text("a much longer value")) }), schema), schema);
        variableUpdated.Values.Should().Equal(
            SqlValue.Integer(1), SqlValue.Text("a much longer value"), SqlValue.Boolean(true), SqlValue.Binary(new byte[] { 4, 5 }));
    }

    [Test]
    public void ApplyUpdate_MultipleColumnsAreAppliedTogetherAndOffsetsAreRebuilt()
    {
        var schema = Schema();
        var encoded = RowCodec.Encode(new Row(new SqlValue[]
        {
            SqlValue.Integer(1), SqlValue.Text("long original"), SqlValue.Boolean(false), SqlValue.Binary(new byte[] { 1 })
        }), schema);
        var update = new RowUpdate(new[]
        {
            new ColumnUpdate(1, SqlValue.Text("x")),
            new ColumnUpdate(2, SqlValue.Boolean(true)),
            new ColumnUpdate(3, SqlValue.Binary(new byte[] { 9, 8, 7, 6 }))
        });

        var actual = RowCodec.Decode(RowCodec.ApplyUpdate(encoded, update, schema), schema);

        actual.Values.Should().Equal(
            SqlValue.Integer(1), SqlValue.Text("x"), SqlValue.Boolean(true), SqlValue.Binary(new byte[] { 9, 8, 7, 6 }));
    }

    [Test]
    public void ApplyUpdate_UnknownDuplicateOrInvalidChangesFailWithoutMutatingInput()
    {
        var schema = Schema();
        var encoded = RowCodec.Encode(new Row(new SqlValue[]
        {
            SqlValue.Integer(1), SqlValue.Text("same"), SqlValue.Boolean(true), SqlValue.Binary(new byte[] { 1 })
        }), schema);
        var original = encoded.ToArray();

        ((Func<byte[]>)(() => RowCodec.ApplyUpdate(encoded,
            new RowUpdate(new[] { new ColumnUpdate(99, SqlValue.Integer(2)) }), schema))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<byte[]>)(() => RowCodec.ApplyUpdate(encoded,
            new RowUpdate(new[] { new ColumnUpdate(0, SqlValue.Integer(2)), new ColumnUpdate(0, SqlValue.Integer(3)) }), schema)))
            .Should().Throw<ArgumentException>();
        ((Func<byte[]>)(() => RowCodec.ApplyUpdate(encoded,
            new RowUpdate(new[] { new ColumnUpdate(0, SqlValue.Text("wrong")) }), schema))).Should().Throw<ArgumentException>();
        ((Func<byte[]>)(() => RowCodec.ApplyUpdate(encoded,
            new RowUpdate(new[] { new ColumnUpdate(0, SqlValue.Null) }), schema))).Should().Throw<ArgumentException>();
        encoded.Should().Equal(original);
    }

    private static TableDefinition Schema() => new(new[]
    {
        new ColumnDefinition(new ColumnId(1), "id", SqlType.Integer, false),
        new ColumnDefinition(new ColumnId(2), "name", SqlType.Text, true),
        new ColumnDefinition(new ColumnId(3), "enabled", SqlType.Boolean, false),
        new ColumnDefinition(new ColumnId(4), "bytes", SqlType.Binary, false)
    });
}
