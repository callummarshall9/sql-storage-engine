using AwesomeAssertions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Rows;

using sql_storage_engine.Models.Rows;
namespace sql_storage_engine.UnitTests;
public sealed class SqlValueCreationArchitectureTests
{
    [Test]
    public void CreationPreservesMarkersSnapshotsAndScalarValidation()
    {
        SqlValueExposure.CreateFromSqlValue(null).IsNull.Should().BeTrue();
        SqlValueExposure.ReadDefaultSqlValue().IsNull.Should().BeFalse();
        var source = new byte[] { 1, 2, 3 };
        var binary = (BinarySqlValue)SqlValueExposure.CreateBinarySqlValue(source);
        source[0] = 99;
        sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadBinarySqlValue(binarySqlValue: binary).ToArray().Should().Equal(1, 2, 3);
        ((Action)(() => SqlValueExposure.CreateFloatSqlValue(double.NaN))).Should().Throw<ArgumentException>();
        ((Action)(() => SqlValueExposure.CreateFromSqlValue(new object()))).Should().Throw<ArgumentException>();
        Parallel.For(0, 64, i => SqlValueExposure.CreateFromSqlValue(i).Should().Be(SqlValueExposure.CreateIntegerSqlValue(i)));
    }

    [Test]
    public void ComparisonValidatesPairsAndKeepsNullSemantics()
    {
        SqlValueExposure.CompareSqlValues([SqlValueExposure.ReadNullSqlValue(), SqlValueExposure.CreateIntegerSqlValue(1)]).Should().Be(SqlComparison.Unknown);
        SqlValueExposure.CompareSqlValues([SqlValueExposure.CreateIntegerSqlValue(1), SqlValueExposure.CreateIntegerSqlValue(2)]).Should().Be(SqlComparison.Less);
        ((Action)(() => SqlValueExposure.CompareSqlValues([]))).Should().Throw<ArgumentException>();
        ((Action)(() => SqlValueExposure.CompareSqlValues([null!, SqlValueExposure.CreateIntegerSqlValue(1)]))).Should().Throw<ArgumentException>();
    }
}