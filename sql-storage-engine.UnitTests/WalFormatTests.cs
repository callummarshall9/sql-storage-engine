using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class WalFormatTests
{
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
