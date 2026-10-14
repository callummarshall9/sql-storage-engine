using System.Buffers.Binary;
using System.Security.Cryptography;
using AwesomeAssertions;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Foundations.UpgradeStates;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class UpgradeStateCodecTests
{
    [TestCase(UpgradeStatus.InProgress, 0)]
    [TestCase(UpgradeStatus.InProgress, 1)]
    [TestCase(UpgradeStatus.Validating, 1)]
    [TestCase(UpgradeStatus.Complete, 1)]
    [TestCase(UpgradeStatus.Failed, 1)]
    public void BinaryCodec_RoundTripsExplicitProgressFields(UpgradeStatus status, int nextStep)
    {
        var service = new UpgradeStateService();
        var context = Context(status, nextStep);
        var encoded = service.EncodeUpgradeContext(context);
        encoded.Bytes!.Length.Should().Be(60);
        service.DecodeUpgradeContext(encoded).Progress.Should().Be(context.Progress);
    }

    [Test]
    public void BinaryCodec_HasStableVersionedBytesAndChecksum()
    {
        var bytes = new UpgradeStateService().EncodeUpgradeContext(Context(UpgradeStatus.Validating, 1)).Bytes!;
        Convert.ToHexString(bytes).Should().Be("5550475253544154010001000C00000000000500010000000200000023E6823EBB7355C5A318FA456008618866EEB989DDD0C00BF9D1E44B15574992");
    }

    [TestCase(0)]
    [TestCase(8)]
    [TestCase(10)]
    [TestCase(12)]
    [TestCase(16)]
    [TestCase(20)]
    [TestCase(24)]
    [TestCase(28)]
    [TestCase(59)]
    public void BinaryCodec_RejectsCorruptedEnvelopePayloadAndChecksum(int offset)
    {
        var service = new UpgradeStateService();
        var encoded = service.EncodeUpgradeContext(Context(UpgradeStatus.Validating, 1));
        encoded.Bytes![offset] ^= 0x80;
        ((Action)(() => service.DecodeUpgradeContext(encoded))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void BinaryCodec_RejectsEveryTruncationAndTrailingBytes()
    {
        var service = new UpgradeStateService();
        var encoded = service.EncodeUpgradeContext(Context(UpgradeStatus.Validating, 1));

        for (var length = 0; length < encoded.Bytes!.Length; length++)
        {
            var truncated = encoded with { Bytes = encoded.Bytes.AsSpan(0, length).ToArray() };
            ((Action)(() => service.DecodeUpgradeContext(truncated))).Should().Throw<StorageFormatException>();
        }

        var extended = encoded with { Bytes = encoded.Bytes.Concat(new byte[] { 0 }).ToArray() };
        ((Action)(() => service.DecodeUpgradeContext(extended))).Should().Throw<StorageFormatException>();
    }

    [TestCase(16)]
    [TestCase(18)]
    [TestCase(20)]
    [TestCase(24)]
    public void BinaryCodec_RejectsInvalidFieldsEvenWithValidChecksum(int offset)
    {
        var service = new UpgradeStateService();
        var encoded = service.EncodeUpgradeContext(Context(UpgradeStatus.Validating, 1));
        BinaryPrimitives.WriteUInt32LittleEndian(encoded.Bytes!.AsSpan(offset), uint.MaxValue);
        SHA256.HashData(encoded.Bytes!.AsSpan(0, 28)).CopyTo(encoded.Bytes!, 28);
        ((Action)(() => service.DecodeUpgradeContext(encoded))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void BinaryCodec_RejectsLegacyJsonBytes()
    {
        var context = Context(UpgradeStatus.InProgress, 0) with { Bytes = "{\"SourceVersion\":0,\"TargetVersion\":5,\"NextStep\":0,\"Status\":1}"u8.ToArray() };
        ((Action)(() => new UpgradeStateService().DecodeUpgradeContext(context))).Should().Throw<StorageFormatException>();
    }

    [TestCase(59)]
    [TestCase(61)]
    [TestCase(16777216)]
    public async Task BoundedReader_RejectsInvalidFileLengthBeforeDecoding(int length)
    {
        var database = Path.Combine(Path.GetTempPath(), "upgrade-state-" + Guid.NewGuid().ToString("N"));
        var path = database + ".upgrade.bin";

        try
        {
            await using (var stream = File.Create(path))
            {
                stream.SetLength(length);
            }

            var context = Context(UpgradeStatus.InProgress, 0) with { DatabasePath = database };
            await ((Func<Task>)(async () => await new UpgradeStateService().ReadUpgradeContextAsync(context, CancellationToken.None))).Should().ThrowAsync<StorageFormatException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static UpgradeContext Context(UpgradeStatus status, int nextStep) => new UpgradeContext
    {
        TargetVersion = DatabaseHeaderFormat.CurrentVersion,
        Progress = new UpgradeProgress
        {
            SourceVersion = 0,
            TargetVersion = DatabaseHeaderFormat.CurrentVersion,
            NextStep = nextStep,
            Status = status
        }
    };
}
