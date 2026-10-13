using sql_storage_engine.Exposures.Backups;
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Exposures.Logging;
using AwesomeAssertions;
using sql_storage_engine.Backup;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.UnitTests;
public sealed class OnlineBackupTests
{
    [Test]
    public async Task WriteMayCompleteAfterBackupStartsAndRestoredCopyIsConsistent()
    {
        using var paths = new Paths();
        await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(paths.Database))
        {
        }

        await File.WriteAllBytesAsync(paths.Wal, WalRecordExposure.WriteWalRecord(new WalRecord { Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 10 }, PreviousLsn = default, TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 }, Type = WalRecordType.Commit, Payload = ReadOnlyMemory<byte>.Empty }));
        var durable = 10UL;
        sql_storage_engine.Exposures.Retention.WalRetentionExposure retention = new sql_storage_engine.Exposures.Retention.WalRetentionExposure();
        var manager = new OnlineBackupExposure(new OfflineBackupExposure(), retention, new sql_storage_engine.Models.Backup.BackupConfiguration { CurrentDurableLsnSource = () => durable });
        var manifest = await manager.CreateBackupManifestAsync(paths.Database, [paths.Wal], paths.Backup, async token =>
        {
            retention.ReadActiveCount().Should().Be(1);
            retention.ReadMinimumRetainedLsn().Should().Be(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 10 });
            await using sql_storage_engine.Exposures.Pages.PageDatabaseExposure writer = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(paths.Database,token);
            await writer.AllocateAsync(PageType.Heap,token);
            durable = 20;
        });
        manifest.StartLsn.Should().Be(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 10 });
        manifest.EndLsn.Should().Be(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 20 });
        manifest.Files.Should().Contain(file => file.Kind == "wal");
        retention.ReadActiveCount().Should().Be(0);
        var restoredPath = await new OfflineBackupExposure().RestoreAsync(paths.Backup,paths.Restore);
        await using sql_storage_engine.Exposures.Pages.PageDatabaseExposure restored = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(restoredPath);
        restored.ReadHeader().NextPageId.Value.Should().Be(2);
    }

    [Test]
    public async Task FailedBackup_ReleasesWalRetentionRegistration()
    {
        using var paths = new Paths();
        await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(paths.Database))
        {
        }

        sql_storage_engine.Exposures.Retention.WalRetentionExposure retention = new sql_storage_engine.Exposures.Retention.WalRetentionExposure();
        var manager = new OnlineBackupExposure(new OfflineBackupExposure(), retention, new sql_storage_engine.Models.Backup.BackupConfiguration { CurrentDurableLsnSource = () => 1 });
        await ((Func<Task>)(async () => await manager.CreateBackupManifestAsync(paths.Database, [Path.Combine(paths.Root, "missing.wal")], paths.Backup))).Should().ThrowAsync<FileNotFoundException>();
        retention.ReadActiveCount().Should().Be(0);
        retention.ReadMinimumRetainedLsn().Should().BeNull();
    }

    [Test]
    public void MultipleBackups_RetainWalFromOldestStart()
    {
        sql_storage_engine.Exposures.Retention.WalRetentionExposure retention = new sql_storage_engine.Exposures.Retention.WalRetentionExposure();
        using var later = retention.Register(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 20 });
        using var earlier = retention.Register(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 10 });
        retention.ReadMinimumRetainedLsn().Should().Be(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 10 });
        earlier.Dispose();
        retention.ReadMinimumRetainedLsn().Should().Be(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 20 });
    }

    private sealed class Paths : IDisposable
    {
        public Paths()
        {
            Root = Path.Combine(Path.GetTempPath(), "sql-online-backup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }
        public string Database => Path.Combine(Root, "source.db");
        public string Wal => Path.Combine(Root, "source.wal");
        public string Backup => Path.Combine(Root, "backup");
        public string Restore => Path.Combine(Root, "restore");

        public void Dispose() => Directory.Delete(Root, true);
    }
}