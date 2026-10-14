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
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class OfflineBackupTests
{
    [Test]
    public async Task BackupManifest_ContainsIdentityFormatSizesChecksumsAndLsnRange()
    {
        using var paths = new BackupPaths();
        var identity = await CreateDatabaseAsync(paths.Database);
        await File.WriteAllBytesAsync(paths.Wal, WalRecordExposure.WriteWalRecord(new WalRecord { Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 7 }, PreviousLsn = default, TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 }, Type = WalRecordType.Commit, Payload = ReadOnlyMemory<byte>.Empty }));
        var manager = Manager();
        var manifest = await manager.CreateBackupManifestAsync(paths.Database,[paths.Wal],paths.Backup);
        File.Exists(Path.Combine(paths.Backup, "manifest.bin")).Should().BeTrue();
        File.Exists(Path.Combine(paths.Backup, "manifest.json")).Should().BeFalse();
        manifest.DatabaseId.Should().Be(identity);
        manifest.FormatVersion.Should().Be(sql_storage_engine.Models.Pages.DatabaseHeaderFormat.CurrentVersion);
        manifest.PageSize.Should().Be(sql_storage_engine.Models.Pages.PageConstants.DefaultSize);
        manifest.StartLsn.Should().Be(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 7 });
        manifest.EndLsn.Should().Be(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 7 });
        manifest.Files.Should().HaveCount(2).And.AllSatisfy(file =>
        {
            file.Size.Should().BeGreaterThan(0);
            file.Sha256.Should().HaveLength(64);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Verification_DetectsModifiedAndMissingFiles(bool delete)
    {
        using var paths = new BackupPaths();
        await CreateDatabaseAsync(paths.Database);
        var manager = Manager();
        await manager.CreateBackupManifestAsync(paths.Database,[],paths.Backup);
        var copiedDatabase = Path.Combine(paths.Backup, "database.db");
        if (delete)
            File.Delete(copiedDatabase);
        else
            await File.AppendAllTextAsync(copiedDatabase, "modified");
        var result = await manager.VerifyAsync(paths.Backup);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.StartsWith(delete ? "FILE_MISSING:" : "SIZE_MISMATCH:", StringComparison.Ordinal));
    }

    [Test]
    public async Task Restore_CreatesSeparateOpenableDatabaseAndRunsPageIntegrityCheck()
    {
        using var paths = new BackupPaths();
        var identity = await CreateDatabaseAsync(paths.Database,allocatePage: true);
        var manager = Manager();
        await manager.CreateBackupManifestAsync(paths.Database,[],paths.Backup);
        var restoredPath = await manager.RestoreAsync(paths.Backup,paths.Restore);
        restoredPath.Should().NotBe(paths.Database);
        await using sql_storage_engine.Exposures.Pages.PageDatabaseExposure restored = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(restoredPath);
        restored.ReadHeader().DatabaseId.Should().Be(identity);
        restored.ReadHeader().NextPageId.Value.Should().Be(2);
    }

    private static OfflineBackupExposure Manager() => new(new sql_storage_engine.Models.Backup.BackupConfiguration { TimeProviderSource = new FixedTimeProvider(new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero)) });
    private static async Task<DatabaseId> CreateDatabaseAsync(string path, bool allocatePage = false)
    {
        await using sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path);
        if (allocatePage)
            await database.AllocateAsync(PageType.Heap);
        return database.ReadHeader().DatabaseId;
    }

    private sealed class BackupPaths : IDisposable
    {
        public BackupPaths()
        {
            Root = Path.Combine(Path.GetTempPath(), "sql-backup-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }
        public string Database => Path.Combine(Root, "source.db");
        public string Wal => Path.Combine(Root, "source.wal");
        public string Backup => Path.Combine(Root, "backup");
        public string Restore => Path.Combine(Root, "restore");

        public void Dispose() => Directory.Delete(Root, true);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}