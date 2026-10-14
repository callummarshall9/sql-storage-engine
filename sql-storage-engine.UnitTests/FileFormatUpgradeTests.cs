using sql_storage_engine.Exposures.Backups;
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;
using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Backup;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Exposures.Diagnostics;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class FileFormatUpgradeTests
{
    [Test]
    public async Task LegacyFixtureUpgrade_ResumesAfterBoundaryAndPreservesExpectedContents()
    {
        using var paths = new Paths();
        await CreateSourceAndBackup(paths);
        MakeLegacy(paths.Database,0);
        var manager = new FileFormatUpgradeExposure();
        await ((Func<Task>)(async () => await manager.UpgradeAsync(paths.Database,paths.Backup,afterBoundary: (_, _) => throw new SimulatedProcessTerminationException(new CrashTermination { Boundary = CrashBoundary.PageWrite, Ordinal = 0 })))).Should().ThrowAsync<SimulatedProcessTerminationException>();
        File.Exists(paths.Database + ".upgrade.bin").Should().BeTrue();
        (await File.ReadAllBytesAsync(paths.Database + ".upgrade.bin")).Length.Should().Be(60);
        File.Exists(paths.Database + ".upgrade.json").Should().BeFalse();
        await manager.UpgradeAsync(paths.Database,paths.Backup,integrityCheck: async (path, token) =>
        {
            await using sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path,DatabaseOpenMode.ReadOnly,token);
            return database.ReadHeader().FormatVersion == sql_storage_engine.Models.Pages.DatabaseHeaderFormat.CurrentVersion;
        });
        manager.ReadCurrentProgress()!.Status.Should().Be(UpgradeStatus.Complete);
        manager.ReadActivity().Should().Contain($"step:0-to-{sql_storage_engine.Models.Pages.DatabaseHeaderFormat.CurrentVersion}").And.Contain($"complete:{sql_storage_engine.Models.Pages.DatabaseHeaderFormat.CurrentVersion}");
        await using sql_storage_engine.Exposures.Pages.PageDatabaseExposure reopened = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(paths.Database,DatabaseOpenMode.ReadOnly);
        reopened.ReadHeader().FormatVersion.Should().Be(sql_storage_engine.Models.Pages.DatabaseHeaderFormat.CurrentVersion);
    }

    [TestCase((ushort)4, (ushort)3)]
    [TestCase((ushort)1, (ushort)0)]
    [TestCase((ushort)1, (ushort)2)]
    public async Task UnsupportedSourceAndDowngradeFailBeforeModification(ushort source, ushort target)
    {
        using var paths = new Paths();
        await CreateSourceAndBackup(paths);
        MakeLegacy(paths.Database,source);
        var before = await File.ReadAllBytesAsync(paths.Database);
        await ((Func<Task>)(async () => await new FileFormatUpgradeExposure().UpgradeAsync(paths.Database,paths.Backup,target))).Should().ThrowAsync<StorageException>();
        (await File.ReadAllBytesAsync(paths.Database)).Should().Equal(before);
        File.Exists(paths.Database + ".upgrade.bin").Should().BeFalse();
    }

    [Test]
    public async Task FailedPostUpgradeIntegrityPreventsCompletion()
    {
        using var paths = new Paths();
        await CreateSourceAndBackup(paths);
        MakeLegacy(paths.Database,0);
        var manager = new FileFormatUpgradeExposure();
        await ((Func<Task>)(async () => await manager.UpgradeAsync(paths.Database, paths.Backup, integrityCheck: (_, _) => Task.FromResult(false)))).Should().ThrowAsync<StorageCorruptionException>();
        manager.ReadCurrentProgress()!.Status.Should().Be(UpgradeStatus.Failed);
        manager.ReadActivity().Should().Contain("integrity:failed");
    }

    [Test]
    public async Task LegacyJsonProgressFailsClosedWithoutRestartingUpgrade()
    {
        using var paths = new Paths();
        await CreateSourceAndBackup(paths);
        MakeLegacy(paths.Database, 0);
        var before = await File.ReadAllBytesAsync(paths.Database);
        await File.WriteAllTextAsync(paths.Database + ".upgrade.json", "{}");
        await ((Func<Task>)(async () => await new FileFormatUpgradeExposure().UpgradeAsync(paths.Database, paths.Backup))).Should().ThrowAsync<StorageFormatException>().WithMessage("*Legacy JSON upgrade progress is unsupported*");
        (await File.ReadAllBytesAsync(paths.Database)).Should().Equal(before);
        File.Exists(paths.Database + ".upgrade.bin").Should().BeFalse();
        File.Exists(paths.Database + ".upgrade.json").Should().BeTrue();
    }

    private static async Task CreateSourceAndBackup(Paths paths)
    {
        await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(paths.Database))
        {
        }

        await new OfflineBackupExposure().CreateBackupManifestAsync(paths.Database,[],paths.Backup);
    }

    private static void MakeLegacy(string path, ushort version)
    {
        var bytes = File.ReadAllBytes(path);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(sql_storage_engine.Models.Pages.DatabaseHeaderFormat.PayloadOffset + 24), version);
        PageChecksumExposure.WriteChecksum(bytes.AsSpan(0, sql_storage_engine.Models.Pages.PageConstants.DefaultSize), sql_storage_engine.Models.Pages.PageConstants.DefaultSize);
        File.WriteAllBytes(path, bytes);
    }

    private sealed class Paths : IDisposable
    {
        public Paths()
        {
            Root = Path.Combine(Path.GetTempPath(), "sql-upgrade-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }
        public string Database => Path.Combine(Root, "source.db");
        public string Backup => Path.Combine(Root, "backup");

        public void Dispose() => Directory.Delete(Root, true);
    }
}