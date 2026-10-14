using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Backups;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Foundations.BackupFiles;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class BackupMetadataCodecTests
{
    private static BackupFileContextService CreateService() =>
        BackupFileContextService.CreateBackupFileContextService(new BackupConfiguration());

    private static BackupManifest Manifest() => new()
    {
        DatabaseId = new DatabaseId { Value = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff") },
        FormatVersion = 5,
        PageSize = 8192,
        StartLsn = new LogSequenceNumber { Value = 123 },
        EndLsn = new LogSequenceNumber { Value = 456 },
        CreatedUtc = new DateTimeOffset(2026, 10, 14, 13, 14, 15, TimeSpan.FromMinutes(330)).AddTicks(1234),
        EngineVersion = "test",
        Files = [new BackupFile { Name = "données.db", Kind = "database", Size = 9876543210, Sha256 = new string('A', 64) }]
    };

    private static byte[] EncodeManifest() =>
        CreateService().SerializeManifestBackupFileContext(new BackupFileContext { Manifest = Manifest() }).Bytes!;

    [Test]
    public void ManifestRoundTripsAllFieldsWithCanonicalGuidAndRawChecksum()
    {
        var bytes = EncodeManifest();
        bytes[..8].Should().Equal("SSEBKP00"u8.ToArray());
        bytes[16..32].Should().Equal(Convert.FromHexString("00112233445566778899AABBCCDDEEFF"));
        bytes.AsSpan(bytes.Length - 64, 32).ToArray().Should().OnlyContain(value => value == 0xAA);
        var decoded = CreateService().DeserializeManifestBackupFileContext(new BackupFileContext { Bytes = bytes }).Manifest!;
        decoded.Should().BeEquivalentTo(Manifest());
        decoded.CreatedUtc.Offset.Should().Be(Manifest().CreatedUtc.Offset);
        decoded.CreatedUtc.Ticks.Should().Be(Manifest().CreatedUtc.Ticks);
        ((IList<BackupFile>)decoded.Files).IsReadOnly.Should().BeTrue();
    }

    [Test]
    public void TimelineRoundTripsAndCannotBeReadAsAManifest()
    {
        var expected = new BackupTimeline { DatabaseId = Manifest().DatabaseId.Value, Timeline = ulong.MaxValue, ParentTimeline = 7, ForkLsn = 9 };
        var service = CreateService();
        var bytes = service.SerializeTimelineBackupFileContext(new BackupFileContext { Timeline = expected }).Bytes!;
        var decoded = service.DeserializeTimelineBackupFileContext(new BackupFileContext { Bytes = bytes }).Timeline!;
        decoded.Should().BeEquivalentTo(expected);
        bytes.Length.Should().Be(88);
        ((Action)(() => service.DeserializeManifestBackupFileContext(new BackupFileContext { Bytes = bytes }))).Should().Throw<StorageFormatException>();
        ((Action)(() => service.DeserializeTimelineBackupFileContext(new BackupFileContext { Bytes = EncodeManifest() }))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void EveryTruncationAndTrailingFileBytesAreRejected()
    {
        var bytes = EncodeManifest();
        var service = CreateService();
        for (var length = 0; length < bytes.Length; length++)
        {
            var truncated = bytes[..length];
            ((Action)(() => service.DeserializeManifestBackupFileContext(new BackupFileContext { Bytes = truncated }))).Should().Throw<StorageFormatException>();
        }
        ((Action)(() => service.DeserializeManifestBackupFileContext(new BackupFileContext { Bytes = [..bytes, 0] }))).Should().Throw<StorageFormatException>();
    }

    [TestCase(0)]
    [TestCase(8)]
    [TestCase(10)]
    [TestCase(12)]
    public void UnknownMarkerVersionTypeAndLengthAreRejected(int position)
    {
        var bytes = EncodeManifest();
        bytes[position] ^= 0x40;
        ((Action)(() => CreateService().DeserializeManifestBackupFileContext(new BackupFileContext { Bytes = bytes }))).Should().Throw<StorageFormatException>();
    }

    [TestCase(64)]
    [TestCase(-1)]
    public void CorruptedPayloadOrChecksumIsRejected(int position)
    {
        var bytes = EncodeManifest();
        bytes[position < 0 ? bytes.Length - 1 : position] ^= 1;
        ((Action)(() => CreateService().DeserializeManifestBackupFileContext(new BackupFileContext { Bytes = bytes }))).Should().Throw<StorageCorruptionException>();
    }

    [TestCase(34)]
    [TestCase(64)]
    [TestCase(72)]
    public void RehashedInvalidPageSizeStringLengthAndFileCountAreRejected(int position)
    {
        var bytes = EncodeManifest();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(position), uint.MaxValue);
        Rehash(bytes);
        ((Action)(() => CreateService().DeserializeManifestBackupFileContext(new BackupFileContext { Bytes = bytes }))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void RehashedInvalidUtf8AndNegativeFileSizesAreRejected()
    {
        var bytes = EncodeManifest();
        bytes[68] = 0xFF;
        Rehash(bytes);
        ((Action)(() => CreateService().DeserializeManifestBackupFileContext(new BackupFileContext { Bytes = bytes }))).Should().Throw<StorageFormatException>();
        bytes = EncodeManifest();
        var sizeOffset = 76 + 4 + Encoding.UTF8.GetByteCount(Manifest().Files[0].Name) + 4 + Encoding.UTF8.GetByteCount("database");
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(sizeOffset), ulong.MaxValue);
        Rehash(bytes);
        ((Action)(() => CreateService().DeserializeManifestBackupFileContext(new BackupFileContext { Bytes = bytes }))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void RehashedTrailingPayloadAndJsonAreRejected()
    {
        var original = EncodeManifest();
        var bytes = new byte[original.Length + 1];
        original.AsSpan(0, original.Length - 32).CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), (uint)(bytes.Length - 48));
        Rehash(bytes);
        ((Action)(() => CreateService().DeserializeManifestBackupFileContext(new BackupFileContext { Bytes = bytes }))).Should().Throw<StorageFormatException>();
        ((Action)(() => CreateService().DeserializeManifestBackupFileContext(new BackupFileContext { Bytes = "{\"DatabaseId\":\"00112233-4455-6677-8899-aabbccddeeff\"}"u8.ToArray() }))).Should().Throw<StorageFormatException>().WithMessage("*legacy JSON*");
    }

    [Test]
    public void EncodingRejectsOverlongInvalidUtf8AndMalformedHashes()
    {
        foreach (var value in new[] { new string('x', 65537), "\uD800" })
        {
            ((Action)(() => CreateService().SerializeManifestBackupFileContext(new BackupFileContext { Manifest = Manifest() with { EngineVersion = value } }))).Should().Throw<StorageFormatException>();
        }
        foreach (var checksum in new[] { "", new string('G', 64) })
        {
            ((Action)(() => CreateService().SerializeManifestBackupFileContext(new BackupFileContext { Manifest = Manifest() with { Files = [Manifest().Files[0] with { Sha256 = checksum }] } }))).Should().Throw<StorageFormatException>();
        }
    }

    [Test]
    public void ManifestFileCountAndAggregatePayloadBoundsApplyBeforePublishing()
    {
        var service = CreateService();
        var manifest = Manifest();
        var tooMany = manifest with { Files = Enumerable.Repeat(manifest.Files[0], 1025).ToArray() };
        ((Action)(() => service.SerializeManifestBackupFileContext(new BackupFileContext { Manifest = tooMany }))).Should().Throw<StorageFormatException>();
        var tooLarge = manifest with { Files = Enumerable.Repeat(manifest.Files[0] with { Name = new string('x', 63000) }, 17).ToArray() };
        ((Action)(() => service.SerializeManifestBackupFileContext(new BackupFileContext { Manifest = tooLarge }))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public async Task MetadataFileReadIsBoundedAndLegacyBackupDirectoryIsExplicitlyRejected()
    {
        var directory = Path.Combine(Path.GetTempPath(), "backup-codec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var oversized = Path.Combine(directory, "oversized.bin");
            using (var stream = File.Create(oversized)) stream.SetLength(1024 * 1024 + 1);
            Func<Task> read = async () => await CreateService().ReadBytesBackupFileContextAsync(new BackupFileContext { Path = oversized }, CancellationToken.None);
            await read.Should().ThrowAsync<StorageFormatException>();
            await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), "{}");
            var verification = await new OfflineBackupExposure().VerifyAsync(directory);
            verification.IsValid.Should().BeFalse();
            verification.Errors.Should().Contain(error => error.Contains("Legacy JSON backups are unsupported", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Rehash(byte[] bytes) =>
        SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32)).CopyTo(bytes.AsSpan(bytes.Length - 32));
}
