using AwesomeAssertions;
using sql_storage_engine.Backup;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class PointInTimeRestoreTests
{
    [Test]
    public async Task ReplayStopsExactlyAtTargetAndLaterTransactionsAreAbsentOnNewTimeline()
    {
        using var paths = new Paths();
        DatabaseId id;
        await using (var database = await PageDatabase.CreateAsync(paths.Database)) id = database.Header.DatabaseId;
        var backup = new OfflineBackupManager();
        await backup.CreateAsync(paths.Database, [], paths.Backup);
        var records = new[]
        {
            new WalRecord(new LogSequenceNumber(1), default, new TransactionId(1), WalRecordType.Begin, ReadOnlyMemory<byte>.Empty),
            new WalRecord(new LogSequenceNumber(41), new LogSequenceNumber(1), new TransactionId(1), WalRecordType.Commit, ReadOnlyMemory<byte>.Empty),
            new WalRecord(new LogSequenceNumber(81), default, new TransactionId(2), WalRecordType.Commit, ReadOnlyMemory<byte>.Empty)
        };
        await File.WriteAllBytesAsync(paths.Segment, PointInTimeRestore.CreateArchiveSegment(
            new WalSegmentHeader(id, 4, 0), records));

        var result = await new PointInTimeRestore(backup).RestoreAsync(paths.Backup, [paths.Segment],
            new LogSequenceNumber(41), paths.Restore);

        result.ReplayedThrough.Should().Be(new LogSequenceNumber(41));
        result.CommittedTransactions.Should().Equal(new TransactionId(1));
        result.Timeline.Should().Be(5);
        File.Exists(Path.Combine(paths.Restore, "timeline.json")).Should().BeTrue();
    }

    [Test]
    public async Task WrongDatabaseAndMissingSegmentsHaveExplicitErrorsBeforeRestore()
    {
        using var paths = new Paths();
        await using (var database = await PageDatabase.CreateAsync(paths.Database)) { }
        var backup = new OfflineBackupManager();
        await backup.CreateAsync(paths.Database, [], paths.Backup);
        var record = new WalRecord(new LogSequenceNumber(1), default, new TransactionId(1), WalRecordType.Commit,
            ReadOnlyMemory<byte>.Empty);
        await File.WriteAllBytesAsync(paths.Segment, PointInTimeRestore.CreateArchiveSegment(
            new WalSegmentHeader(DatabaseId.New(), 1, 0), [record]));
        var restore = new PointInTimeRestore(backup);
        await ((Func<Task>)(async () => await restore.RestoreAsync(paths.Backup, [paths.Segment],
            new LogSequenceNumber(1), paths.Restore))).Should().ThrowAsync<WrongDatabaseWalException>();
        await ((Func<Task>)(async () => await restore.RestoreAsync(paths.Backup, [],
            new LogSequenceNumber(1), paths.Restore))).Should().ThrowAsync<MissingWalSegmentException>();
        Directory.Exists(paths.Restore).Should().BeFalse();
    }

    private sealed class Paths : IDisposable
    {
        public Paths() { Root = Path.Combine(Path.GetTempPath(), "sql-pitr-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Root); }
        public string Root { get; }
        public string Database => Path.Combine(Root, "source.db");
        public string Backup => Path.Combine(Root, "backup");
        public string Restore => Path.Combine(Root, "restore");
        public string Segment => Path.Combine(Root, "000.wal");
        public void Dispose() => Directory.Delete(Root, true);
    }
}
