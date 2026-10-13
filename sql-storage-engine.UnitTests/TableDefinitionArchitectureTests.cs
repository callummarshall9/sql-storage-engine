using AwesomeAssertions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;
public sealed class TableDefinitionArchitectureTests
{
    [Test]
    public void SchemaMaterializesOnceAndSnapshotsItsColumns()
    {
        var enumerations = 0;
        var column = ColumnDefinitionExposure.CreateColumnDefinition(new ColumnId { Value = 1 },"value",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),false);
        IEnumerable<ColumnDefinition> Source() { enumerations++; yield return column; }
        var schema = TableDefinitionExposure.CreateTableDefinition(Source());
        enumerations.Should().Be(1);
        TableDefinitionExposure.CheckTableDefinitionRow(schema,RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3)]));
        enumerations.Should().Be(1);
        var original = new[] { column };
        var snapshot = TableDefinitionExposure.CreateTableDefinition(original);
        original[0] = column with { Name = "changed" };
        snapshot.Columns[0].Name.Should().Be("value");
        ((IList<ColumnDefinition>)snapshot.Columns).IsReadOnly.Should().BeTrue();
    }

    [Test]
    public void RawInvalidSchemaAndSparseColumnsAreRejectedAtAdmission()
    {
        var column = new ColumnDefinition { Id = new ColumnId { Value = 1 }, Name = "value", Type = sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), IsNullable = false };
        var row = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)]);
        var duplicate = new TableDefinition { Columns = [column, column with { Name = "other" }] };
        ((Action)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(row,duplicate))).Should().Throw<ArgumentException>();
        var sparse = new TableDefinition { Columns = [column with { IsSparse = true }] };
        ((Action)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]),sparse))).Should().Throw<ArgumentException>();
        ((Action)(() => ColumnDefinitionExposure.CheckColumnDefinitionValue(column,sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void NullInputsRemainValidationFailures()
    {
        var column = ColumnDefinitionExposure.CreateColumnDefinition(new ColumnId { Value = 1 },"value",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),false);
        var schema = TableDefinitionExposure.CreateTableDefinition([column]);
        ((Action)(() => TableDefinitionExposure.CheckTableDefinitionRow(schema,null!))).Should().Throw<ArgumentException>();
        ((Action)(() => TableDefinitionExposure.CheckTableDefinitionRow(null!,RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)])))).Should().Throw<ArgumentException>();
    }
}