using System.Buffers.Binary;
using System.Security.Cryptography;
using AwesomeAssertions;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Services.Foundations.ObjectManifests;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class StorageObjectManifestTests
{
    private static StorageObjectManifest Manifest() => new()
    {
        DatabaseId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
        Commit = 17,
        PageSize = 4096,
        SegmentSize = 8192,
        FileLength = 12288,
        Segments =
        [
            new StorageObjectSegment { Index = 0, Length = 8192, Key = "db/segments/first", Checksum = SHA256.HashData(new byte[8192]) },
            new StorageObjectSegment { Index = 1, Length = 4096, Key = "db/segments/second", Checksum = SHA256.HashData(new byte[4096]) }
        ]
    };

    private static byte[] Encode(StorageObjectManifest? manifest = null) =>
        new StorageObjectManifestService().EncodeStorageStrategyContext(new StorageStrategyContext { Manifest = manifest ?? Manifest() }).Bytes!;

    private static StorageObjectManifest Decode(byte[] bytes) =>
        new StorageObjectManifestService().DecodeStorageStrategyContext(new StorageStrategyContext { Bytes = bytes }).Manifest!;

    [Test]
    public void RoundTripPreservesGeometryAndImmutableSegmentReferences()
    {
        var manifest = Manifest();
        var bytes = Encode(manifest);
        Decode(bytes).Should().BeEquivalentTo(manifest);
        bytes.AsSpan(0, 8).ToArray().Should().Equal("SSEOBJ01"u8.ToArray());
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8)).Should().Be(1);
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10)).Should().Be(1);
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12)).Should().Be((uint)(bytes.Length - 48));
        Convert.ToHexString(bytes.AsSpan(16, 16)).Should().Be("00112233445566778899AABBCCDDEEFF");
        bytes.Length.Should().Be(215);
        BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(32)).Should().Be(17);
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(40)).Should().Be(4096);
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(44)).Should().Be(8192);
        BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(48)).Should().Be(12288);
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(56)).Should().Be(2);
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(60)).Should().Be(0);
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(64)).Should().Be(8192);
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(68)).Should().Be(17);
        bytes.AsSpan(72, 17).ToArray().Should().Equal("db/segments/first"u8.ToArray());
        bytes.AsSpan(89, 32).ToArray().Should().Equal(manifest.Segments![0].Checksum!);
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(121)).Should().Be(1);
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(125)).Should().Be(4096);
        bytes.AsSpan(151, 32).ToArray().Should().Equal(manifest.Segments![1].Checksum!);
        bytes.AsSpan(bytes.Length - 32).ToArray().Should().Equal(SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32)));
    }

    [Test]
    public void EveryTruncationAndTrailingByteIsRejected()
    {
        var bytes = Encode();
        for (var length = 0; length < bytes.Length; length++)
            ((Action)(() => Decode(bytes[..length]))).Should().Throw<StorageFormatException>();
        ((Action)(() => Decode([.. bytes, 0]))).Should().Throw<StorageFormatException>();
    }

    [TestCase(0)]
    [TestCase(8)]
    [TestCase(10)]
    [TestCase(12)]
    [TestCase(32)]
    [TestCase(-1)]
    public void EnvelopeAndPayloadCorruptionIsRejected(int position)
    {
        var bytes = Encode();
        bytes[position < 0 ? bytes.Length - 1 : position] ^= 1;
        ((Action)(() => Decode(bytes))).Should().Throw<StorageFormatException>();
    }

    [TestCase(32)]
    [TestCase(40)]
    [TestCase(44)]
    [TestCase(48)]
    [TestCase(56)]
    [TestCase(60)]
    [TestCase(64)]
    [TestCase(68)]
    public void InvalidRehashedCommitGeometryAndSegmentFieldsAreRejected(int position)
    {
        var bytes = Encode();
        bytes.AsSpan(position, position == 32 ? 8 : 4).Fill(255);
        SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32)).CopyTo(bytes.AsSpan(bytes.Length - 32));
        ((Action)(() => Decode(bytes))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void InvalidUtf8AndOversizedKeysAreRejected()
    {
        var bytes = Encode();
        bytes[72] = 255;
        SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32)).CopyTo(bytes.AsSpan(bytes.Length - 32));
        ((Action)(() => Decode(bytes))).Should().Throw<StorageFormatException>();
        var manifest = Manifest();
        manifest.Segments![0] = manifest.Segments[0] with { Key = new string('x', 1025) };
        ((Action)(() => Encode(manifest))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void InvalidManifestCannotBeEncoded()
    {
        var manifest = Manifest();
        ((Action)(() => Encode(manifest with { FileLength = 12289 }))).Should().Throw<StorageFormatException>();
        ((Action)(() => Encode(manifest with { DatabaseId = Guid.Empty }))).Should().Throw<StorageFormatException>();
        ((Action)(() => Encode(manifest with { Segments = [] }))).Should().Throw<StorageFormatException>();
        manifest.Segments![1] = manifest.Segments[1] with { Index = 2 };
        ((Action)(() => Encode(manifest))).Should().Throw<StorageFormatException>();
    }
}