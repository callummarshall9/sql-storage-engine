// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Exposures.Logging;
using AwesomeAssertions;
using System.Buffers.Binary;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Logging;

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
        var bytes = WalRecordExposure.WriteWalRecord(new sql_storage_engine.Models.Logging.WalRecord { Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 10 }, PreviousLsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 4 }, TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId { Value = 2 }, Type = WalRecordType.PageChange, Payload = payload });
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(36)).Should().Be(checksum);
        WalRecordExposure.ReadWalRecord(bytes).Payload.ToArray().Should().Equal(payload);
        bytes[36] ^= 1;
        ((Func<WalRecord>)(() => WalRecordExposure.ReadWalRecord(bytes))).Should().Throw<StorageCorruptionException>();
        bytes[36] ^= 1;
        bytes[^1] ^= 1;
        ((Func<WalRecord>)(() => WalRecordExposure.ReadWalRecord(bytes))).Should().Throw<StorageCorruptionException>();
    }

    [TestCase(WalRecordType.Begin)]
    [TestCase(WalRecordType.PageChange)]
    [TestCase(WalRecordType.Commit)]
    [TestCase(WalRecordType.Rollback)]
    [TestCase(WalRecordType.Checkpoint)]
    [TestCase(WalRecordType.Compensation)]
    public void EveryRecordType_RoundTrips(WalRecordType type)
    {
        var record = new WalRecord
        {
            Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 10
            },
            PreviousLsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 4
            },
            TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
            {
                Value = 2
            },
            Type = type,
            Payload = new byte[]
            {
                1,
                2
            }
        };
        var decoded = WalRecordExposure.ReadWalRecord(WalRecordExposure.WriteWalRecord(record));
        (decoded with
        {
            Payload = record.Payload
        }

        ).Should().Be(record);
        decoded.Payload.ToArray().Should().Equal(record.Payload.ToArray());
    }

    [Test]
    public void SegmentHeader_RoundTripsAndHasGoldenBytes()
    {
        var header = new WalSegmentHeader
        {
            DatabaseId = new sql_storage_engine.Models.Identifiers.DatabaseId
            {
                Value = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")
            },
            Timeline = 2,
            SegmentNumber = 3
        };
        var bytes = WalSegmentHeaderExposure.WriteWalSegmentHeader(header);
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(44)).Should().Be(0x99469d1fu);
        WalSegmentHeaderExposure.ReadWalSegmentHeader(bytes).Should().Be(header);
        Convert.ToHexString(bytes[..40]).Should().Be("57414C310100000000112233445566778899AABBCCDDEEFF02000000000000000300000000000000");
    }

    [Test]
    public void UnknownVersionsTypesAndModifiedBytes_AreRejected()
    {
        var bytes = WalRecordExposure.WriteWalRecord(new WalRecord { Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 1 }, PreviousLsn = default, TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 }, Type = WalRecordType.Begin, Payload = new byte[] { 1 } });
        bytes[4] = 2;
        ((Func<WalRecord>)(() => WalRecordExposure.ReadWalRecord(bytes))).Should().Throw<StorageFormatException>();
        bytes = WalRecordExposure.WriteWalRecord(new WalRecord { Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 1 }, PreviousLsn = default, TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 }, Type = WalRecordType.Begin, Payload = new byte[] { 1 } });
        bytes[6] = 99;
        ((Func<WalRecord>)(() => WalRecordExposure.ReadWalRecord(bytes))).Should().Throw<StorageFormatException>();
        bytes = WalRecordExposure.WriteWalRecord(new WalRecord { Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 1 }, PreviousLsn = default, TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 }, Type = WalRecordType.Begin, Payload = new byte[] { 1 } });
        bytes[^1] ^= 1;
        ((Func<WalRecord>)(() => WalRecordExposure.ReadWalRecord(bytes))).Should().Throw<StorageCorruptionException>();
    }

    [Test]
    public void IncompleteTailIsDistinguishedFromMidLogCorruption()
    {
        var record = WalRecordExposure.WriteWalRecord(new WalRecord { Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 1 }, PreviousLsn = default, TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 }, Type = WalRecordType.Begin, Payload = new byte[] { 1 } });
        WalReadResultExposure.ReadWalReadResult(record[..^1]).HasIncompleteTail.Should().BeTrue();
        var two = record.Concat(record).ToArray();
        two[10] ^= 1;
        ((Func<WalReadResult>)(() => WalReadResultExposure.ReadWalReadResult(two))).Should().Throw<StorageCorruptionException>();
    }
}