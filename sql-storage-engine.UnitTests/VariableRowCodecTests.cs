using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class VariableRowCodecTests
{
    [Test]
    public void EmptyAndMultibyteTextAndBinary_RoundTripWithoutConversion()
    {
        var schema = Schema();
        var expected = RowExposure.CreateRow(new SqlValue[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(string.Empty), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 0, 0xff, 0x80 }), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("héllø 🌍") });
        var actual = sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(expected,schema),schema);
        actual.Values.Should().Equal(expected.Values);
    }

    [Test]
    public void DecodeSelected_ReturnsOnlyRequestedFixedAndVariableColumns()
    {
        var schema = Schema();
        var encoded = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow(new SqlValue[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(9), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("skip"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 4, 5 }), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue() }),schema);
        var selected = sql_storage_engine.Exposures.Rows.RowCodecExposure.DecodeSelected(encoded,schema,new[] { new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 } });
        selected.Keys.Should().BeEquivalentTo(new[] { new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 } });
        selected[new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(9));
        selected[new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 4, 5 }));
        selected[new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 }].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue());
    }

    [Test]
    public void Decode_RejectsOverlappingDecreasingAndOutOfRangeVariableEntries()
    {
        var schema = Schema();
        var original = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow(new SqlValue[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("abc"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 1, 2 }), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("z") }),schema);
        var tableOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(12)));
        var overlapping = original.ToArray();
        var firstOffset = BinaryPrimitives.ReadUInt32LittleEndian(overlapping.AsSpan(tableOffset + 4));
        BinaryPrimitives.WriteUInt32LittleEndian(overlapping.AsSpan(tableOffset + sql_storage_engine.Exposures.Rows.RowCodecExposure.VariableEntryLength + 4), firstOffset + 1);
        ((Func<Row>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(overlapping,schema))).Should().Throw<StorageFormatException>();
        var decreasing = original.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(decreasing.AsSpan(tableOffset + sql_storage_engine.Exposures.Rows.RowCodecExposure.VariableEntryLength + 4), firstOffset - 1);
        ((Func<Row>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(decreasing,schema))).Should().Throw<StorageFormatException>();
        var outside = original.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(outside.AsSpan(tableOffset + 8), uint.MaxValue);
        ((Func<Row>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(outside,schema))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void Encode_EnforcesMaximumInlineValueLength()
    {
        var schema = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition(new[] { sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"bytes",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeVarBinaryMax(),false) });
        var oversized = new byte[sql_storage_engine.Exposures.Rows.RowCodecExposure.MaximumInlineValueLength + 1];
        ((Func<byte[]>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow(new[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(oversized) }),schema))).Should().Throw<ArgumentException>();
    }

    private static TableDefinition Schema() => sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition(new[] { sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"id",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"name",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(4000),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },"bytes",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeVarBinaryMax(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 },"note",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(4000),true) });
}