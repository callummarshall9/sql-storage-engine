using System.Buffers.Binary;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Exposures.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class PageChecksumCompatibilityTests
{
    // Golden IEEE CRC-32 values independently generated using Python zlib.crc32.
    [TestCase(4096, 0x179b2344u)]
    [TestCase(8192, 0xebea42fau)]
    [TestCase(16384, 0x0d38a2f0u)]
    [TestCase(32768, 0x8bda623cu)]
    [TestCase(65536, 0xea0d361du)]
    public void ChecksumsMatchExistingPageFormatAndRejectCorruption(int size, uint expected)
    {
        var page = Enumerable.Range(0, size).Select(i => (byte)((i * 37 + 11) % 256)).ToArray();
        // An existing stored checksum is excluded from validation and overwritten on write.
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(sql_storage_engine.Models.Pages.PageHeaderFormat.ChecksumOffset), expected);
        Assert.DoesNotThrow(() => PageChecksumExposure.ValidateChecksum(page, size));
        PageChecksumExposure.WriteChecksum(page, size);
        Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(page.AsSpan(sql_storage_engine.Models.Pages.PageHeaderFormat.ChecksumOffset)), Is.EqualTo(expected));
        Assert.DoesNotThrow(() => PageChecksumExposure.ValidateChecksum(page, size));
        page[^1] ^= 0x80;
        Assert.Throws<StorageCorruptionException>(() => PageChecksumExposure.ValidateChecksum(page, size));
        page[^1] ^= 0x80;
        page[sql_storage_engine.Models.Pages.PageHeaderFormat.ChecksumOffset] ^= 1;
        Assert.Throws<StorageCorruptionException>(() => PageChecksumExposure.ValidateChecksum(page, size));
    }
}