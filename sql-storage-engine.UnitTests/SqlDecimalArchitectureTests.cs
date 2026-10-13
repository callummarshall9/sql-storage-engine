using System.Numerics;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;
public sealed class SqlDecimalArchitectureTests
{
    [Test]
    public void RoundingAndRescalingPreserveSignAndExactness()
    {
        foreach(var sign in new[] { -1, 1 })
        {
            var value = SqlDecimalExposure.CreateSqlDecimal(125 * sign,2);
            SqlDecimalExposure.TryRescaleSqlDecimal(value,1,out var truncated).Should().BeFalse();
            truncated.Should().Be(new BigInteger(12 * sign));
            SqlDecimalExposure.RoundSqlDecimalToScale(value,1,out var rounded);
            rounded.Should().Be(new BigInteger(13 * sign));
            SqlDecimalExposure.TryRescaleSqlDecimal(value,4,out var expanded).Should().BeTrue();
            expanded.Should().Be(new BigInteger(12500 * sign));
            SqlDecimalExposure.FormatSqlDecimal(value).Should().Be(sign < 0 ? "-1.25" : "1.25");
        }
        var maximum = SqlDecimalExposure.ParseSqlDecimal(new string('9', 38));
        SqlDecimalExposure.FormatSqlDecimal(maximum).Should().Be(new string('9', 38));
        ((Action)(() => SqlDecimalExposure.FormatSqlDecimal(new SqlDecimal { Coefficient = BigInteger.Pow(10,38) }))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void NumericEqualityAndHashRemainSeparateFromRepresentation()
    {
        var first = SqlDecimalExposure.CreateSqlDecimal(120,2);
        var second = SqlDecimalExposure.CreateSqlDecimal(12,1);
        first.Should().NotBe(second);
        SqlDecimalExposure.CheckEquivalentSqlDecimals([first, second]).Should().BeTrue();
        SqlDecimalExposure.HashSqlDecimal(first).Should().Be(SqlDecimalExposure.HashSqlDecimal(second));
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CompareSqlValues([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(first), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(second)]).Should().Be(sql_storage_engine.Rows.SqlComparison.Equal);
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(first).Should().NotBe(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(second));
        Parallel.For(0, 64, _ => SqlDecimalExposure.CompareSqlDecimals([first, second]).Should().Be(0));
    }
}