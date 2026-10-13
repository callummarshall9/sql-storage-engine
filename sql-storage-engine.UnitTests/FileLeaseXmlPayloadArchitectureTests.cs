using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Files;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class FileLeaseXmlPayloadArchitectureTests
{
    [Test]
    public void FileLeaseKeepsItsPersistentInodeAndReleasesUnderConcurrentDisposal()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "database");
        try
        {
            var lease = StorageFileLeaseExposure.Acquire(path, creating: true);
            File.Exists(path + ".storage-lock").Should().BeTrue();
            ((Action)(() => StorageFileLeaseExposure.Acquire(path, creating: false))).Should().Throw<IOException>().Which.InnerException.Should().BeAssignableTo<IOException>();
            Parallel.For(0, 64, _ => lease.Dispose());
            File.Exists(path + ".storage-lock").Should().BeTrue();
            using var next = StorageFileLeaseExposure.Acquire(path, creating: false);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestCase("")]
    [TestCase("<root attribute=\"😀\">é漢字</root>")]
    public void XmlPayloadPreservesExactVersionedCompressedBytes(string xml)
    {
        using var reference = new MemoryStream();
        reference.WriteByte(1);
        using (var compressor = new BrotliStream(reference, CompressionLevel.Optimal, leaveOpen: true))
            compressor.Write(new UTF8Encoding(false, true).GetBytes(xml));
        var encoded = XmlPayloadExposure.EncodeXmlPayload(xml);
        encoded.Should().Equal(reference.ToArray());
        XmlPayloadExposure.DecodeXmlPayload(encoded).Should().Be(xml);
    }

    [Test]
    public void XmlPayloadRetainsStrictUtf8FailureCauseAndSupportsConcurrentCalls()
    {
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        using (var compressor = new BrotliStream(stream, CompressionLevel.Optimal, leaveOpen: true)) compressor.Write(new byte[] { 255 });
        ((Func<string>)(() => XmlPayloadExposure.DecodeXmlPayload(stream.ToArray()))).Should().Throw<StorageFormatException>().Which.InnerException.Should().BeOfType<DecoderFallbackException>();
        ((Func<string>)(() => XmlPayloadExposure.DecodeXmlPayload(new byte[] { 0 }))).Should().Throw<StorageFormatException>();
        Parallel.For(0, 64, index =>
        {
            var xml = $"<root>{index}</root>";
            XmlPayloadExposure.DecodeXmlPayload(XmlPayloadExposure.EncodeXmlPayload(xml)).Should().Be(xml);
        });
    }
}