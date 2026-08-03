using AwesomeAssertions;
using sql_storage_engine.Backup;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class OfflineBackupTests
{
    [Test]
    public async Task BackupManifest_ContainsIdentityFormatSizesChecksumsAndLsnRange()
    {
        using var paths = new BackupPaths();
        var identity = await CreateDatabaseAsync(paths.Database);
        await File.WriteAllBytesAsync(paths.Wal, WalFormat.WriteRecord(new WalRecord(new LogSequenceNumber(7),
            default, new TransactionId(1), WalRecordType.Commit, ReadOnlyMemory<byte>.Empty)));
        var manager = Manager();

        var manifest = await manager.CreateAsync(paths.Database, [paths.Wal], paths.Backup);

        manifest.DatabaseId.Should().Be(identity);
        manifest.FormatVersion.Should().Be(DatabaseHeader.CurrentFormatVersion);
        manifest.PageSize.Should().Be(PageConstants.DefaultSize);
        manifest.StartLsn.Should().Be(new LogSequenceNumber(7));
        manifest.EndLsn.Should().Be(new LogSequenceNumber(7));
        manifest.Files.Should().HaveCount(2).And.AllSatisfy(file =>
        { file.Size.Should().BeGreaterThan(0); file.Sha256.Should().HaveLength(64); });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Verification_DetectsModifiedAndMissingFiles(bool delete)
    {
        using var paths = new BackupPaths();
        await CreateDatabaseAsync(paths.Database);
        var manager = Manager();
        await manager.CreateAsync(paths.Database, [], paths.Backup);
        var copiedDatabase = Path.Combine(paths.Backup, "database.db");
        if (delete) File.Delete(copiedDatabase);
        else await File.AppendAllTextAsync(copiedDatabase, "modified");

        var result = await manager.VerifyAsync(paths.Backup);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.StartsWith(delete ? "FILE_MISSING:" : "SIZE_MISMATCH:",
            StringComparison.Ordinal));
    }

    [Test]
    public async Task Restore_CreatesSeparateOpenableDatabaseAndRunsPageIntegrityCheck()
    {
        using var paths = new BackupPaths();
        var identity = await CreateDatabaseAsync(paths.Database, allocatePage: true);
        var manager = Manager();
        await manager.CreateAsync(paths.Database, [], paths.Backup);

        var restoredPath = await manager.RestoreAsync(paths.Backup, paths.Restore);

        restoredPath.Should().NotBe(paths.Database);
        await using var restored = await PageDatabase.OpenAsync(restoredPath);
        restored.Header.DatabaseId.Should().Be(identity);
        restored.Header.NextPageId.Value.Should().Be(2);
    }

    private static OfflineBackupManager Manager() => new(new FixedTimeProvider(
        new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero)));

    private static async Task<DatabaseId> CreateDatabaseAsync(string path, bool allocatePage = false)
    {
        await using var database = await PageDatabase.CreateAsync(path);
        if (allocatePage) await database.AllocateAsync(PageType.Heap);
        return database.Header.DatabaseId;
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
