using System.Buffers.Binary;
using System.Text.Json;
using sql_storage_engine.Backup;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Diagnostics;

public enum UpgradeStatus { InProgress = 1, Validating = 2, Complete = 3, Failed = 4 }
public sealed record UpgradeProgress(ushort SourceVersion, ushort TargetVersion, int NextStep, UpgradeStatus Status);

/// <summary>Runs ordered, idempotent format migrations with durable progress and verified-backup protection.</summary>
public sealed class FileFormatUpgradeManager(OfflineBackupManager backupManager)
{
    private readonly List<string> _activity = [];
    public IReadOnlyList<string> Activity => _activity.AsReadOnly();
    public UpgradeProgress? CurrentProgress { get; private set; }

    public async Task UpgradeAsync(string databasePath, string verifiedBackupDirectory,
        ushort targetVersion = DatabaseHeader.CurrentFormatVersion,
        Func<int, CancellationToken, ValueTask>? afterBoundary = null,
        Func<string, CancellationToken, Task<bool>>? integrityCheck = null,
        CancellationToken cancellationToken = default)
    {
        var progressPath = databasePath + ".upgrade.json";
        var page = await File.ReadAllBytesAsync(databasePath, cancellationToken).ConfigureAwait(false);
        if (page.Length < 64) throw new StorageFormatException("Upgrade source is truncated.");
        var pageSize = BinaryPrimitives.ReadInt32LittleEndian(page.AsSpan(DatabaseHeaderCodec.PayloadOffset + 28));
        if (!PageConstants.IsSupportedSize(pageSize) || page.Length < pageSize) throw new InvalidPageSizeException(pageSize);
        var sourceVersion = BinaryPrimitives.ReadUInt16LittleEndian(page.AsSpan(DatabaseHeaderCodec.PayloadOffset + 24));
        if (targetVersion != DatabaseHeader.CurrentFormatVersion || sourceVersion > DatabaseHeader.CurrentFormatVersion)
            throw new UnsupportedDatabaseVersionException(sourceVersion);
        if (targetVersion < sourceVersion) throw new StorageFormatException("Database format downgrade is not supported.");
        var verification = await backupManager.VerifyAsync(verifiedBackupDirectory, cancellationToken).ConfigureAwait(false);
        if (!verification.IsValid) throw new StorageCorruptionException("A verified backup is required before upgrade.");
        UpgradeProgress progress;
        if (File.Exists(progressPath))
            progress = JsonSerializer.Deserialize<UpgradeProgress>(await File.ReadAllBytesAsync(progressPath, cancellationToken).ConfigureAwait(false))
                ?? throw new StorageFormatException("Upgrade progress is empty.");
        else
        {
            if (sourceVersion is not (0 or DatabaseHeader.CurrentFormatVersion))
                throw new UnsupportedDatabaseVersionException(sourceVersion);
            progress = new(sourceVersion, targetVersion, 0, UpgradeStatus.InProgress);
            await PersistAsync(progressPath, progress, cancellationToken).ConfigureAwait(false);
        }
        CurrentProgress = progress; _activity.Add($"resume:{progress.NextStep}");
        if (progress.NextStep == 0 && progress.SourceVersion == 0)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(DatabaseHeaderCodec.PayloadOffset + 24), 1);
            PageChecksum.WriteChecksum(page.AsSpan(0, pageSize), pageSize);
            await File.WriteAllBytesAsync(databasePath, page, cancellationToken).ConfigureAwait(false);
            progress = progress with { NextStep = 1 }; CurrentProgress = progress;
            await PersistAsync(progressPath, progress, cancellationToken).ConfigureAwait(false);
            _activity.Add("step:0-to-1");
            if (afterBoundary is not null) await afterBoundary(1, cancellationToken).ConfigureAwait(false);
        }
        progress = progress with { Status = UpgradeStatus.Validating }; CurrentProgress = progress;
        await PersistAsync(progressPath, progress, cancellationToken).ConfigureAwait(false);
        var valid = integrityCheck is null || await integrityCheck(databasePath, cancellationToken).ConfigureAwait(false);
        if (!valid)
        {
            progress = progress with { Status = UpgradeStatus.Failed }; CurrentProgress = progress;
            await PersistAsync(progressPath, progress, cancellationToken).ConfigureAwait(false);
            _activity.Add("integrity:failed");
            throw new StorageCorruptionException("Post-upgrade integrity validation failed.");
        }
        progress = progress with { Status = UpgradeStatus.Complete }; CurrentProgress = progress;
        await PersistAsync(progressPath, progress, cancellationToken).ConfigureAwait(false);
        _activity.Add("complete:1");
    }

    private static async Task PersistAsync(string path, UpgradeProgress progress, CancellationToken token)
    {
        var temporary = path + ".tmp";
        await File.WriteAllBytesAsync(temporary, JsonSerializer.SerializeToUtf8Bytes(progress), token).ConfigureAwait(false);
        File.Move(temporary, path, true);
    }
}
