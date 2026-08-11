using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Backup;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class FileFormatUpgradeTests
{
    [Test]
    public async Task LegacyFixtureUpgrade_ResumesAfterBoundaryAndPreservesExpectedContents()
    {
        using var paths = new Paths();
        await CreateSourceAndBackup(paths);
        MakeLegacy(paths.Database, 0);
        var manager = new FileFormatUpgradeManager(new OfflineBackupManager());
        await ((Func<Task>)(async () => await manager.UpgradeAsync(paths.Database, paths.Backup,
            afterBoundary: (_, _) => throw new SimulatedProcessTerminationException(CrashBoundary.PageWrite, 0))))
            .Should().ThrowAsync<SimulatedProcessTerminationException>();

        await manager.UpgradeAsync(paths.Database, paths.Backup, integrityCheck: async (path, token) =>
        { await using var database = await PageDatabase.OpenAsync(path, DatabaseOpenMode.ReadOnly, token); return database.Header.FormatVersion == DatabaseHeader.CurrentFormatVersion; });

        manager.CurrentProgress!.Status.Should().Be(UpgradeStatus.Complete);
        manager.Activity.Should().Contain($"step:0-to-{DatabaseHeader.CurrentFormatVersion}")
            .And.Contain($"complete:{DatabaseHeader.CurrentFormatVersion}");
        await using var reopened = await PageDatabase.OpenAsync(paths.Database, DatabaseOpenMode.ReadOnly);
        reopened.Header.FormatVersion.Should().Be(DatabaseHeader.CurrentFormatVersion);
    }

    [TestCase((ushort)4, (ushort)3)]
    [TestCase((ushort)1, (ushort)0)]
    [TestCase((ushort)1, (ushort)2)]
    public async Task UnsupportedSourceAndDowngradeFailBeforeModification(ushort source, ushort target)
    {
        using var paths = new Paths(); await CreateSourceAndBackup(paths); MakeLegacy(paths.Database, source);
        var before = await File.ReadAllBytesAsync(paths.Database);
        await ((Func<Task>)(async () => await new FileFormatUpgradeManager(new OfflineBackupManager())
            .UpgradeAsync(paths.Database, paths.Backup, target))).Should().ThrowAsync<StorageException>();
        (await File.ReadAllBytesAsync(paths.Database)).Should().Equal(before);
        File.Exists(paths.Database + ".upgrade.json").Should().BeFalse();
    }

    [Test]
    public async Task FailedPostUpgradeIntegrityPreventsCompletion()
    {
        using var paths = new Paths(); await CreateSourceAndBackup(paths); MakeLegacy(paths.Database, 0);
        var manager = new FileFormatUpgradeManager(new OfflineBackupManager());
        await ((Func<Task>)(async () => await manager.UpgradeAsync(paths.Database, paths.Backup,
            integrityCheck: (_, _) => Task.FromResult(false)))).Should().ThrowAsync<StorageCorruptionException>();
        manager.CurrentProgress!.Status.Should().Be(UpgradeStatus.Failed);
        manager.Activity.Should().Contain("integrity:failed");
    }

    private static async Task CreateSourceAndBackup(Paths paths)
    { await using (var database = await PageDatabase.CreateAsync(paths.Database)) { } await new OfflineBackupManager().CreateAsync(paths.Database, [], paths.Backup); }
    private static void MakeLegacy(string path, ushort version)
    { var bytes = File.ReadAllBytes(path); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(DatabaseHeaderCodec.PayloadOffset + 24), version); PageChecksum.WriteChecksum(bytes.AsSpan(0, PageConstants.DefaultSize), PageConstants.DefaultSize); File.WriteAllBytes(path, bytes); }
    private sealed class Paths : IDisposable
    {
        public Paths() { Root = Path.Combine(Path.GetTempPath(), "sql-upgrade-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Root); }
        public string Root { get; } public string Database => Path.Combine(Root, "source.db"); public string Backup => Path.Combine(Root, "backup");
        public void Dispose() => Directory.Delete(Root, true);
    }
}
