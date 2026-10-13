using sql_storage_engine.Exposures.Backups;
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Backup;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Exposures.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class PointInTimeRestoreTests
{
    [Test]
    public async Task ReplayStopsExactlyAtTargetAndLaterTransactionsAreAbsentOnNewTimeline()
    {
        using var paths = new Paths();
        DatabaseId id;
        await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(paths.Database))
            id = database.ReadHeader().DatabaseId;
        var backup = new OfflineBackupExposure();
        await backup.CreateBackupManifestAsync(paths.Database,[],paths.Backup);
        var records = new[]
        {
            new WalRecord
            {
                Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
                {
                    Value = 1
                },
                PreviousLsn = default,
                TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
                {
                    Value = 1
                },
                Type = WalRecordType.Begin,
                Payload = ReadOnlyMemory<byte>.Empty
            },
            new WalRecord
            {
                Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
                {
                    Value = 41
                },
                PreviousLsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
                {
                    Value = 1
                },
                TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
                {
                    Value = 1
                },
                Type = WalRecordType.Commit,
                Payload = ReadOnlyMemory<byte>.Empty
            },
            new WalRecord
            {
                Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
                {
                    Value = 81
                },
                PreviousLsn = default,
                TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
                {
                    Value = 2
                },
                Type = WalRecordType.Commit,
                Payload = ReadOnlyMemory<byte>.Empty
            }
        };
        await File.WriteAllBytesAsync(paths.Segment, PointInTimeRestoreExposure.CreateArchiveSegment(new WalSegmentHeader { DatabaseId = id, Timeline = 4, SegmentNumber = 0 },records));
        var result = await new PointInTimeRestoreExposure(backup).RestoreAsync(paths.Backup,[paths.Segment],new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 41 },paths.Restore);
        result.ReplayedThrough.Should().Be(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 41 });
        result.CommittedTransactions.Should().Equal(new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 });
        result.Timeline.Should().Be(5);
        File.Exists(Path.Combine(paths.Restore, "timeline.json")).Should().BeTrue();
    }

    [Test]
    public async Task WrongDatabaseAndMissingSegmentsHaveExplicitErrorsBeforeRestore()
    {
        using var paths = new Paths();
        await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(paths.Database))
        {
        }

        var backup = new OfflineBackupExposure();
        await backup.CreateBackupManifestAsync(paths.Database,[],paths.Backup);
        var record = new WalRecord
        {
            Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = 1
            },
            PreviousLsn = default,
            TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
            {
                Value = 1
            },
            Type = WalRecordType.Commit,
            Payload = ReadOnlyMemory<byte>.Empty
        };
        await File.WriteAllBytesAsync(paths.Segment, PointInTimeRestoreExposure.CreateArchiveSegment(new WalSegmentHeader { DatabaseId = DatabaseIdExposure.CreateDatabaseId(), Timeline = 1, SegmentNumber = 0 },[record]));
        var restore = new PointInTimeRestoreExposure(backup);
        await ((Func<Task>)(async () => await restore.RestoreAsync(paths.Backup,[paths.Segment],new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 1 },paths.Restore))).Should().ThrowAsync<WrongDatabaseWalException>();
        await ((Func<Task>)(async () => await restore.RestoreAsync(paths.Backup,[],new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 1 },paths.Restore))).Should().ThrowAsync<MissingWalSegmentException>();
        Directory.Exists(paths.Restore).Should().BeFalse();
    }

    private sealed class Paths : IDisposable
    {
        public Paths()
        {
            Root = Path.Combine(Path.GetTempPath(), "sql-pitr-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }
        public string Database => Path.Combine(Root, "source.db");
        public string Backup => Path.Combine(Root, "backup");
        public string Restore => Path.Combine(Root, "restore");
        public string Segment => Path.Combine(Root, "000.wal");

        public void Dispose() => Directory.Delete(Root, true);
    }
}