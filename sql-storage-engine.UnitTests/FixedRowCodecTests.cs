using AwesomeAssertions;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class FixedRowCodecTests
{
    [Test]
    public void FixedValuesAndNullCombinations_RoundTrip()
    {
        var schema = Schema();
        var rows = new[]
        {
            RowExposure.CreateRow(new[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(false), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(long.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue() }),
            RowExposure.CreateRow(new[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(long.MaxValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(0) })
        };
        foreach (var expected in rows)
        {
            var actual = sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(expected,schema),schema);
            actual.Values.Should().Equal(expected.Values);
        }
    }

    [Test]
    public void BoundaryValues_HaveGoldenLittleEndianBytes()
    {
        var schema = Schema();
        var encoded = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow(new[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(long.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue() }),schema);
        Convert.ToHexString(encoded.AsSpan(32)).Should().Be("040100000000000000800000000000000000");
        encoded.Length.Should().Be(50);
        BitConverter.ToUInt16(encoded, 0).Should().Be(4);
        BitConverter.ToUInt16(encoded, 2).Should().Be(3);
        BitConverter.ToUInt16(encoded, 4).Should().Be(1);
    }

    [Test]
    public void Encode_RejectsColumnCountTypeAndNullabilityMismatch()
    {
        var schema = Schema();
        ((Func<byte[]>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow(new[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true) }),schema))).Should().Throw<ArgumentException>();
        ((Func<byte[]>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow(new[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3) }),schema))).Should().Throw<ArgumentException>();
        ((Func<byte[]>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow(new[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3) }),schema))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void Decode_RejectsTruncationSchemaMismatchAndUncheckedLengths()
    {
        var schema = Schema();
        var encoded = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow(new[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue() }),schema);
        ((Func<Row>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(encoded.AsSpan(0, 31),schema))).Should().Throw<StorageFormatException>();
        ((Func<Row>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(encoded.AsSpan(0, encoded.Length - 1),schema))).Should().Throw<StorageFormatException>();
        var otherSchema = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition(new[] { sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 99 },"active",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBit(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"minimum",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },"optional",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(),true) });
        ((Func<Row>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(encoded,otherSchema))).Should().Throw<StorageFormatException>();
        encoded[20] = 0xff;
        ((Func<Row>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(encoded,schema))).Should().Throw<StorageFormatException>();
    }

    private static TableDefinition Schema() => sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition(new[] { sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"active",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBit(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"minimum",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },"optional",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(),true) });
}