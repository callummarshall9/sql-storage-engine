// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class RowUpdateTests
{
    [Test]
    public void ApplyUpdate_OneFixedOrVariableColumnPreservesEveryOtherValue()
    {
        var schema = Schema();
        var original = RowExposure.CreateRow(new SqlValue[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("old"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 4, 5 }) });
        var encoded = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(original,schema);
        var fixedUpdated = sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sql_storage_engine.Exposures.Rows.RowCodecExposure.ApplyUpdate(encoded,RowUpdateExposure.CreateRowUpdate(new[] { new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2) } }),schema),schema);
        fixedUpdated.Values.Should().Equal(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("old"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 4, 5 }));
        var variableUpdated = sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sql_storage_engine.Exposures.Rows.RowCodecExposure.ApplyUpdate(encoded,RowUpdateExposure.CreateRowUpdate(new[] { new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("a much longer value") } }),schema),schema);
        variableUpdated.Values.Should().Equal(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("a much longer value"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 4, 5 }));
    }

    [Test]
    public void ApplyUpdate_MultipleColumnsAreAppliedTogetherAndOffsetsAreRebuilt()
    {
        var schema = Schema();
        var encoded = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow(new SqlValue[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("long original"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(false), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 1 }) }),schema);
        var update = RowUpdateExposure.CreateRowUpdate(new[] { new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("x") }, new ColumnUpdate { ColumnIndex = 2, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true) }, new ColumnUpdate { ColumnIndex = 3, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 9, 8, 7, 6 }) } });
        var actual = sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sql_storage_engine.Exposures.Rows.RowCodecExposure.ApplyUpdate(encoded,update,schema),schema);
        actual.Values.Should().Equal(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("x"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 9, 8, 7, 6 }));
    }

    [Test]
    public void ApplyUpdate_UnknownDuplicateOrInvalidChangesFailWithoutMutatingInput()
    {
        var schema = Schema();
        var encoded = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow(new SqlValue[] { sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("same"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 1 }) }),schema);
        var original = encoded.ToArray();
        ((Func<byte[]>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.ApplyUpdate(encoded,RowUpdateExposure.CreateRowUpdate(new[] { new ColumnUpdate { ColumnIndex = 99, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2) } }),schema))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<byte[]>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.ApplyUpdate(encoded,RowUpdateExposure.CreateRowUpdate(new[] { new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2) }, new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3) } }),schema))).Should().Throw<ArgumentException>();
        ((Func<byte[]>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.ApplyUpdate(encoded,RowUpdateExposure.CreateRowUpdate(new[] { new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("wrong") } }),schema))).Should().Throw<ArgumentException>();
        ((Func<byte[]>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.ApplyUpdate(encoded,RowUpdateExposure.CreateRowUpdate(new[] { new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue() } }),schema))).Should().Throw<ArgumentException>();
        encoded.Should().Equal(original);
    }

    private static TableDefinition Schema() => sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition(new[] { sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"id",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"name",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(4000),true), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },"enabled",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBit(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 },"bytes",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeVarBinaryMax(),false) });
}