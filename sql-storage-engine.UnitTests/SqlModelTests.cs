using AwesomeAssertions;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Identifiers;

using sql_storage_engine.Models.Rows;
namespace sql_storage_engine.UnitTests;
public sealed class SqlModelTests
{
    [Test]
    public void EveryInitialType_HasTypedRepresentationAndNullIsDistinctFromDefaults()
    {
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(false).Should().BeOfType<BooleanSqlValue>();
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(0).Should().BeOfType<IntegerSqlValue>();
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(string.Empty).Should().BeOfType<TextSqlValue>();
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(ReadOnlySpan<byte>.Empty).Should().BeOfType<BinarySqlValue>();
        sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue().Should().BeOfType<NullSqlValue>();
        sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue().Should().NotBe(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(false));
        sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue().Should().NotBe(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(0));
        sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue().IsNull.Should().BeTrue();
    }

    [Test]
    public void Schema_RejectsNullForNonNullableAndMismatchedTypes()
    {
        var column = sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"enabled",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBit(),false);
        ((Action)(() => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CheckColumnDefinitionValue(columnDefinition: column,sqlValue: sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()))).Should().Throw<ArgumentException>();
        ((Action)(() => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CheckColumnDefinitionValue(columnDefinition: column,sqlValue: sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)))).Should().Throw<ArgumentException>();
        ((Action)(() => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CheckColumnDefinitionValue(columnDefinition: column,sqlValue: sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true)))).Should().NotThrow();
    }

    [Test]
    public void RuntimeConversion_AcceptsSupportedRepresentationsAndRejectsUnknownOnes()
    {
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFromSqlValue(12.5).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(12.5));
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFromSqlValue(42).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(42));
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFromSqlValue((byte)42).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(42));
        ((Func<SqlValue>)(() => sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFromSqlValue(ulong.MaxValue))).Should().Throw<ArgumentException>();
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFromSqlValue(42L).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(42));
    }

    [Test]
    public void Comparison_UsesDocumentedNullAndTypeRules()
    {
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CompareSqlValues(sqlValues: [sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()]).Should().Be(SqlComparison.Unknown);
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CompareSqlValues(sqlValues: [sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)]).Should().Be(SqlComparison.Less);
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CompareSqlValues(sqlValues: [sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("a"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("a")]).Should().Be(SqlComparison.Equal);
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CompareSqlValues(sqlValues: [sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 1, 2 }), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[] { 1, 3 })]).Should().Be(SqlComparison.Less);
        ((Func<SqlComparison>)(() => sql_storage_engine.Exposures.Rows.SqlValueExposure.CompareSqlValues(sqlValues: [sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("1")]))).Should().Throw<ArgumentException>();
    }
}