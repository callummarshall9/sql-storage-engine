using AwesomeAssertions;
using sql_storage_engine.Brokers.Checksums;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class Crc32BrokerTests
{
    [Test]
    public void IeeeReferenceVectorAndEmptyInputMatchTheDurablePolynomial()
    {
        var broker = new Crc32Broker();
        broker.CalculateChecksum("123456789"u8).Should().Be(0xcbf43926u);
        broker.CalculateChecksum([]).Should().Be(0u);
    }

    [Test]
    public void FragmentedZeroFieldMatchesAnIndependentScalarImplementationWithoutMutatingInput()
    {
        var random = new Random(7331);
        var broker = new Crc32Broker();
        foreach (var length in new[]
        {
            4,
            5,
            7,
            8,
            9,
            31,
            255,
            1024,
            8192,
            32768
        }

        )
        {
            var bytes = new byte[length];
            random.NextBytes(bytes);
            foreach (var offset in new[]
            {
                0,
                (length - 4) / 2,
                length - 4
            }

            )
            {
                var expectedBytes = bytes.ToArray();
                expectedBytes.AsSpan(offset, 4).Clear();
                var original = bytes.ToArray();
                broker.CalculateChecksumWithZeroedField(bytes,offset).Should().Be(ScalarCrc(expectedBytes));
                bytes.Should().Equal(original);
            }
        }
    }

    [Test]
    public void WritingPageChecksumsDoesNotAllocatePerOperationAfterWarmup()
    {
        var page = new byte[8192];
        for (var attempt = 0; attempt < 32; attempt++)
            PageChecksumExposure.WriteChecksum(page,page.Length);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var attempt = 0; attempt < 64; attempt++)
            PageChecksumExposure.WriteChecksum(page,page.Length);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
        PageChecksumExposure.ValidateChecksum(page,page.Length);
    }

    private static uint ScalarCrc(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xffffffffu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }

        return ~crc;
    }
}