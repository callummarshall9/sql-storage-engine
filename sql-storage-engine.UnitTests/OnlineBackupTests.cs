using AwesomeAssertions;
using sql_storage_engine.Backup;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class OnlineBackupTests
{
    [Test]
    public async Task WriteMayCompleteAfterBackupStartsAndRestoredCopyIsConsistent()
    {
        using var paths = new Paths();
        await using (var database = await PageDatabase.CreateAsync(paths.Database)) { }
        await File.WriteAllBytesAsync(paths.Wal, WalFormat.WriteRecord(new WalRecord(new LogSequenceNumber(10),
            default, new TransactionId(1), WalRecordType.Commit, ReadOnlyMemory<byte>.Empty)));
        var durable = 10UL;
        var retention = new WalRetentionRegistry();
        var manager = new OnlineBackupManager(new OfflineBackupManager(), retention,
            () => new LogSequenceNumber(durable));

        var manifest = await manager.CreateAsync(paths.Database, [paths.Wal], paths.Backup, async token =>
        {
            retention.ActiveCount.Should().Be(1);
            retention.MinimumRetainedLsn.Should().Be(new LogSequenceNumber(10));
            await using var writer = await PageDatabase.OpenAsync(paths.Database, token);
            await writer.AllocateAsync(PageType.Heap, token);
            durable = 20;
        });

        manifest.StartLsn.Should().Be(new LogSequenceNumber(10));
        manifest.EndLsn.Should().Be(new LogSequenceNumber(20));
        manifest.Files.Should().Contain(file => file.Kind == "wal");
        retention.ActiveCount.Should().Be(0);
        var restoredPath = await new OfflineBackupManager().RestoreAsync(paths.Backup, paths.Restore);
        await using var restored = await PageDatabase.OpenAsync(restoredPath);
        restored.Header.NextPageId.Value.Should().Be(2);
    }

    [Test]
    public async Task FailedBackup_ReleasesWalRetentionRegistration()
    {
        using var paths = new Paths();
        await using (var database = await PageDatabase.CreateAsync(paths.Database)) { }
        var retention = new WalRetentionRegistry();
        var manager = new OnlineBackupManager(new OfflineBackupManager(), retention,
            () => new LogSequenceNumber(1));

        await ((Func<Task>)(async () => await manager.CreateAsync(paths.Database,
            [Path.Combine(paths.Root, "missing.wal")], paths.Backup))).Should().ThrowAsync<FileNotFoundException>();

        retention.ActiveCount.Should().Be(0);
        retention.MinimumRetainedLsn.Should().BeNull();
    }

    [Test]
    public void MultipleBackups_RetainWalFromOldestStart()
    {
        var retention = new WalRetentionRegistry();
        using var later = retention.Register(new LogSequenceNumber(20));
        using var earlier = retention.Register(new LogSequenceNumber(10));
        retention.MinimumRetainedLsn.Should().Be(new LogSequenceNumber(10));
        earlier.Dispose();
        retention.MinimumRetainedLsn.Should().Be(new LogSequenceNumber(20));
    }

    private sealed class Paths : IDisposable
    {
        public Paths() { Root = Path.Combine(Path.GetTempPath(), "sql-online-backup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Root); }
        public string Root { get; }
        public string Database => Path.Combine(Root, "source.db");
        public string Wal => Path.Combine(Root, "source.wal");
        public string Backup => Path.Combine(Root, "backup");
        public string Restore => Path.Combine(Root, "restore");
        public void Dispose() => Directory.Delete(Root, true);
    }
}
