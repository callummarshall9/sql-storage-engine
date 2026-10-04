using AwesomeAssertions;
using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class WalFormatTests
{
    // Independent IEEE CRC-32 golden values from Python zlib.crc32 over the
    // version-one little-endian record envelope, with its checksum bytes zero.
    [TestCase(0, 0x48234977u)]
    [TestCase(1, 0x63ee6172u)]
    [TestCase(7, 0xf3289d4cu)]
    [TestCase(8, 0xa134c05bu)]
    [TestCase(9, 0x1e921d34u)]
    [TestCase(15, 0x71c7d9a3u)]
    [TestCase(16, 0xee55aa02u)]
    [TestCase(17, 0xd8f18113u)]
    [TestCase(256, 0xaf806dd1u)]
    [TestCase(65536, 0xefaaf561u)]
    [TestCase(1048576, 0x596cf98du)]
    public void RecordChecksum_MatchesIndependentGoldenAndRejectsCorruption(int length, uint checksum)
    {
        var payload = Enumerable.Range(0, length).Select(i => (byte)(i * 37 + 11)).ToArray();
        var bytes = WalFormat.WriteRecord(new(new(10), new(4), new(2), WalRecordType.PageChange, payload));
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(36)).Should().Be(checksum);
        WalFormat.ReadRecord(bytes).Payload.ToArray().Should().Equal(payload);
        bytes[36] ^= 1;
        ((Func<WalRecord>)(() => WalFormat.ReadRecord(bytes))).Should().Throw<StorageCorruptionException>();
        bytes[36] ^= 1;
        bytes[^1] ^= 1;
        ((Func<WalRecord>)(() => WalFormat.ReadRecord(bytes))).Should().Throw<StorageCorruptionException>();
    }

    [TestCase(WalRecordType.Begin)] [TestCase(WalRecordType.PageChange)] [TestCase(WalRecordType.Commit)]
    [TestCase(WalRecordType.Rollback)] [TestCase(WalRecordType.Checkpoint)]
    public void EveryRecordType_RoundTrips(WalRecordType type)
    {
        var record = new WalRecord(new LogSequenceNumber(10), new LogSequenceNumber(4), new TransactionId(2), type, new byte[] { 1, 2 });
        var decoded = WalFormat.ReadRecord(WalFormat.WriteRecord(record));
        (decoded with { Payload = record.Payload }).Should().Be(record);
        decoded.Payload.ToArray().Should().Equal(record.Payload.ToArray());
    }

    [Test]
    public void SegmentHeader_RoundTripsAndHasGoldenBytes()
    {
        var header = new WalSegmentHeader(new DatabaseId(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")), 2, 3);
        var bytes = WalFormat.WriteSegmentHeader(header);
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(44)).Should().Be(0x99469d1fu);
        WalFormat.ReadSegmentHeader(bytes).Should().Be(header);
        Convert.ToHexString(bytes[..40]).Should().Be("57414C310100000000112233445566778899AABBCCDDEEFF02000000000000000300000000000000");
    }

    [Test]
    public void UnknownVersionsTypesAndModifiedBytes_AreRejected()
    {
        var bytes = WalFormat.WriteRecord(new WalRecord(new LogSequenceNumber(1), default, new TransactionId(1), WalRecordType.Begin, new byte[] { 1 }));
        bytes[4] = 2; ((Func<WalRecord>)(() => WalFormat.ReadRecord(bytes))).Should().Throw<StorageFormatException>();
        bytes = WalFormat.WriteRecord(new WalRecord(new LogSequenceNumber(1), default, new TransactionId(1), WalRecordType.Begin, new byte[] { 1 }));
        bytes[6] = 99; ((Func<WalRecord>)(() => WalFormat.ReadRecord(bytes))).Should().Throw<StorageFormatException>();
        bytes = WalFormat.WriteRecord(new WalRecord(new LogSequenceNumber(1), default, new TransactionId(1), WalRecordType.Begin, new byte[] { 1 }));
        bytes[^1] ^= 1; ((Func<WalRecord>)(() => WalFormat.ReadRecord(bytes))).Should().Throw<StorageCorruptionException>();
    }

    [Test]
    public void IncompleteTailIsDistinguishedFromMidLogCorruption()
    {
        var record = WalFormat.WriteRecord(new WalRecord(new LogSequenceNumber(1), default, new TransactionId(1), WalRecordType.Begin, new byte[] { 1 }));
        WalFormat.ReadRecords(record[..^1]).HasIncompleteTail.Should().BeTrue();
        var two = record.Concat(record).ToArray(); two[10] ^= 1;
        ((Func<WalReadResult>)(() => WalFormat.ReadRecords(two))).Should().Throw<StorageCorruptionException>();
    }
}
