using System.Runtime.InteropServices;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;
public sealed class BinaryValueArchitectureTests
{
    [Test]
    public void AllByteValuesKeepEqualityAndIndependentReadSnapshots()
    {
        var bytes = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var first = (BinarySqlValue)SqlValueExposure.CreateBinarySqlValue(bytes);
        var second = (BinarySqlValue)SqlValueExposure.CreateBinarySqlValue(bytes);
        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
        var read = SqlValueExposure.ReadBinarySqlValue(first);
        MemoryMarshal.TryGetArray(read,out var segment).Should().BeTrue();
        segment.Array![0] = 99;
        bytes[1] = 99;
        SqlValueExposure.ReadBinarySqlValue(first).ToArray().Should().Equal(Enumerable.Range(0, 256).Select(i => (byte)i));
        Parallel.For(0, 64, _ => SqlValueExposure.ReadBinarySqlValue(first).Length.Should().Be(256));
    }

    [Test]
    public void RawInvalidContentIsRejectedAtReadAndRowAdmission()
    {
        var invalid = new BinarySqlValue { StorageFamily = SqlStorageFamily.Binary, Content = "\u0100" };
        ((Action)(() => SqlValueExposure.ReadBinarySqlValue(invalid))).Should().Throw<ArgumentException>();
        ((Action)(() => RowExposure.CreateRow([invalid]))).Should().Throw<ArgumentException>();
    }
}