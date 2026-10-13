using AwesomeAssertions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;
public sealed class ScalarValueArchitectureTests
{
    [Test]
    public void ScalarCreationPreservesFamilyAndNormalization()
    {
        var negativeZero = (FloatSqlValue)SqlValueExposure.CreateFloatSqlValue(-0d);
        BitConverter.DoubleToInt64Bits(negativeZero.Value).Should().Be(0);
        negativeZero.StorageFamily.Should().Be(SqlStorageFamily.Float);
        var utc = new DateTime(2026, 10, 13, 12, 30, 0, DateTimeKind.Utc);
        var temporal = (DateTimeSqlValue)SqlValueExposure.CreateDateTimeSqlValue(utc);
        temporal.Value.Ticks.Should().Be(utc.Ticks);
        temporal.Value.Kind.Should().Be(DateTimeKind.Unspecified);
        RowExposure.CreateRow([negativeZero, temporal]).Values.Should().HaveCount(2);
    }

    [Test]
    public void RowAdmissionRejectsContradictoryRawScalarModels()
    {
        foreach(var value in new SqlValue[] {
            new IntegerSqlValue { Value = 1, StorageFamily = SqlStorageFamily.Text },
            new FloatSqlValue { Value = double.NaN, StorageFamily = SqlStorageFamily.Float },
            new TextSqlValue { StorageFamily = SqlStorageFamily.Text },
            new NullSqlValue { IsNull = false },
            new BooleanSqlValue { Value = true, StorageFamily = SqlStorageFamily.Boolean, IsNull = true }
        })
            ((Action)(() => RowExposure.CreateRow([value]))).Should().Throw<ArgumentException>();
        RowExposure.CreateRow([new IntegerSqlValue { Value = 1, StorageFamily = SqlStorageFamily.Integer }]).Values.Should().HaveCount(1);
    }
}