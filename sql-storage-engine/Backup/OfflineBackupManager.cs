using System.Security.Cryptography;
using System.Text.Json;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Backup;

public sealed record BackupFile(string Name, string Kind, long Size, string Sha256);

/// <summary>Describes the identity, format, recovery interval, and independently verifiable files in a backup.</summary>
public sealed record BackupManifest(DatabaseId DatabaseId, ushort FormatVersion, int PageSize,
    LogSequenceNumber StartLsn, LogSequenceNumber EndLsn, DateTimeOffset CreatedUtc,
    string EngineVersion, IReadOnlyList<BackupFile> Files);

public sealed record BackupVerificationResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>Creates, verifies, and restores physical backups of cleanly closed databases.</summary>
public sealed class OfflineBackupManager(TimeProvider? timeProvider = null)
{
    public const string ManifestFileName = "manifest.json";
    private const int MaximumManifestBytes = 1024 * 1024;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<BackupManifest> CreateAsync(string databasePath, IEnumerable<string> walPaths,
        string destinationDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(walPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        var destination = Path.GetFullPath(destinationDirectory);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException($"Backup destination already exists: '{destination}'.");
        DatabaseHeader header;
        await using (var database = await PageDatabase.OpenAsync(databasePath, cancellationToken).ConfigureAwait(false))
            header = database.Header;
        if (!header.IsCleanShutdown)
            throw new StorageResourceException("Offline backup requires a cleanly closed database.", new InvalidOperationException());

        var sources = new List<(string Path, string Name, string Kind)>
        { (Path.GetFullPath(databasePath), "database.db", "database") };
        var index = 0;
        foreach (var walPath in walPaths)
            sources.Add((Path.GetFullPath(walPath), $"wal-{index++:D6}.log", "wal"));
        if (sources.Select(source => source.Path).Distinct(StringComparer.Ordinal).Count() != sources.Count)
            throw new ArgumentException("Backup source files must be unique.", nameof(walPaths));

        Directory.CreateDirectory(destination);
        try
        {
            var files = new List<BackupFile>();
            var lsns = new List<LogSequenceNumber>();
            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(source.Path)) throw new FileNotFoundException("A required backup file is missing.", source.Path);
                var target = Path.Combine(destination, source.Name);
                await CopyAsync(source.Path, target, cancellationToken).ConfigureAwait(false);
                files.Add(await DescribeAsync(target, source.Name, source.Kind, cancellationToken).ConfigureAwait(false));
                if (source.Kind == "wal") lsns.AddRange(await ReadLsnsAsync(target, cancellationToken).ConfigureAwait(false));
            }
            var manifest = new BackupManifest(header.DatabaseId, header.FormatVersion, header.PageSize,
                lsns.Count == 0 ? default : lsns.MinBy(lsn => lsn.Value),
                lsns.Count == 0 ? default : lsns.MaxBy(lsn => lsn.Value), _timeProvider.GetUtcNow(),
                typeof(OfflineBackupManager).Assembly.GetName().Version?.ToString() ?? "0.0.0", files.AsReadOnly());
            await File.WriteAllBytesAsync(Path.Combine(destination, ManifestFileName),
                JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions), cancellationToken).ConfigureAwait(false);
            return manifest;
        }
        catch
        {
            Directory.Delete(destination, true);
            throw;
        }
    }

    public async Task<BackupVerificationResult> VerifyAsync(string backupDirectory,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        BackupManifest manifest;
        try { manifest = await ReadManifestAsync(backupDirectory, cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (exception is IOException or JsonException or StorageException)
        { return new BackupVerificationResult(false, ["MANIFEST_INVALID:" + exception.Message]); }
        foreach (var file in manifest.Files)
        {
            var path = SafeBackupPath(backupDirectory, file.Name);
            if (!File.Exists(path)) { errors.Add($"FILE_MISSING:{file.Name}"); continue; }
            var actual = await DescribeAsync(path, file.Name, file.Kind, cancellationToken).ConfigureAwait(false);
            if (actual.Size != file.Size) errors.Add($"SIZE_MISMATCH:{file.Name}");
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual.Sha256), Convert.FromHexString(file.Sha256)))
                errors.Add($"CHECKSUM_MISMATCH:{file.Name}");
        }
        return new BackupVerificationResult(errors.Count == 0, errors.AsReadOnly());
    }

    public async Task<string> RestoreAsync(string backupDirectory, string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        var verification = await VerifyAsync(backupDirectory, cancellationToken).ConfigureAwait(false);
        if (!verification.IsValid) throw new StorageCorruptionException(string.Join(';', verification.Errors));
        var manifest = await ReadManifestAsync(backupDirectory, cancellationToken).ConfigureAwait(false);
        var destination = Path.GetFullPath(destinationDirectory);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException($"Restore destination already exists: '{destination}'.");
        Directory.CreateDirectory(destination);
        try
        {
            foreach (var file in manifest.Files)
                await CopyAsync(SafeBackupPath(backupDirectory, file.Name), Path.Combine(destination, file.Name), cancellationToken)
                    .ConfigureAwait(false);
            var databasePath = Path.Combine(destination, manifest.Files.Single(file => file.Kind == "database").Name);
            await VerifyDatabasePagesAsync(databasePath, manifest, cancellationToken).ConfigureAwait(false);
            return databasePath;
        }
        catch { Directory.Delete(destination, true); throw; }
    }

    public async Task<BackupManifest> ReadManifestAsync(string backupDirectory,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(Path.GetFullPath(backupDirectory), ManifestFileName);
        var info = new FileInfo(path);
        if (!info.Exists) throw new StorageFormatException("Backup manifest is missing.");
        if (info.Length is <= 0 or > MaximumManifestBytes) throw new StorageFormatException("Backup manifest size is invalid.");
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var manifest = JsonSerializer.Deserialize<BackupManifest>(bytes, JsonOptions) ??
            throw new StorageFormatException("Backup manifest is empty.");
        if (!PageConstants.IsSupportedSize(manifest.PageSize) || manifest.Files.Count is 0 or > 1024 ||
            manifest.Files.Any(file => file.Size < 0 || file.Name != Path.GetFileName(file.Name) || file.Sha256.Length != 64) ||
            manifest.Files.Count(file => file.Kind == "database") != 1)
            throw new StorageFormatException("Backup manifest fields are invalid.");
        return manifest;
    }

    private static async Task VerifyDatabasePagesAsync(string path, BackupManifest manifest, CancellationToken token)
    {
        await using var database = await PageDatabase.OpenAsync(path, token).ConfigureAwait(false);
        if (database.Header.DatabaseId != manifest.DatabaseId || database.Header.FormatVersion != manifest.FormatVersion ||
            database.PageSize != manifest.PageSize) throw new StorageCorruptionException("Restored database identity or format differs from its manifest.");
        var page = new byte[database.PageSize];
        for (ulong value = 0; value < database.Header.NextPageId.Value; value++)
        {
            await database.ReadAsync(new PageId(value), page, token).ConfigureAwait(false);
            PageChecksum.ValidateChecksum(page, database.PageSize);
            PageHeaderCodec.Read(page).Validate(new PageId(value));
        }
    }

    private static string SafeBackupPath(string root, string name)
    {
        if (name != Path.GetFileName(name)) throw new StorageFormatException("Backup file name escapes its directory.");
        return Path.Combine(Path.GetFullPath(root), name);
    }

    private static async Task CopyAsync(string source, string destination, CancellationToken token)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await input.CopyToAsync(output, token).ConfigureAwait(false);
        await output.FlushAsync(token).ConfigureAwait(false);
        output.Flush(true);
    }

    private static async Task<BackupFile> DescribeAsync(string path, string name, string kind, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var checksum = await SHA256.HashDataAsync(stream, token).ConfigureAwait(false);
        return new BackupFile(name, kind, stream.Length, Convert.ToHexString(checksum));
    }

    private static async Task<IReadOnlyList<LogSequenceNumber>> ReadLsnsAsync(string path, CancellationToken token)
    {
        var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
        return WalFormat.ReadRecords(bytes).Records.Select(record => record.Lsn).ToArray();
    }
}
