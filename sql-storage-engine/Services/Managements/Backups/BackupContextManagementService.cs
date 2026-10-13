// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Backup;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Logging;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Coordinations.BackupFiles;
using sql_storage_engine.Services.Coordinations.PageDatabases;

namespace sql_storage_engine.Services.Managements.Backups;
internal sealed partial class BackupContextManagementService : IBackupContextManagementService
{
    private readonly BackupFileContextCoordinationService fileService;
    private readonly PageDatabaseContextCoordinationService pageDatabaseService;
    private readonly BackupConfiguration configuration;
    private BackupContextManagementService(BackupFileContextCoordinationService fileService, PageDatabaseContextCoordinationService pageDatabaseService, BackupConfiguration configuration)
    {
        this.fileService = fileService;
        this.pageDatabaseService = pageDatabaseService;
        this.configuration = configuration;
    }

    internal static BackupContextManagementService CreateBackupContextManagementService(BackupConfiguration configuration) =>
        new(fileService: BackupFileContextCoordinationService.CreateBackupFileContextCoordinationService(backupConfiguration: configuration), pageDatabaseService: PageDatabaseContextCoordinationService.CreatePageDatabaseContextCoordinationService(), configuration: configuration);

    public BackupFileContext ReadBackupFileContextBinding(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            ValueSource = configuration
        };
    });

    public ValueTask<BackupFileContext> CreateBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch<BackupFileContext>(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return backupFileContext with
        {
            Manifest = await CreateBackupManifestPhysicalAsync(databasePath: backupFileContext.DatabasePath!, walPaths: backupFileContext.WalPaths!, destinationDirectory: backupFileContext.DestinationDirectory!, requireCleanShutdown: true, startLsn: null, endLsn: null, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
        };
    });

    public ValueTask<BackupFileContext> CreateOnlineBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch<BackupFileContext>(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);
        var registered = fileService.RegisterBackupFileContextRetention(backupFileContext: backupFileContext);

        try
        {
            var started = await fileService.StartBackupFileContextCopyAsync(backupFileContext: registered, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return started with
            {
                Manifest = await CreateBackupManifestPhysicalAsync(databasePath: started.DatabasePath!, walPaths: started.WalPaths!, destinationDirectory: started.DestinationDirectory!, requireCleanShutdown: false, startLsn: started.StartLsn, endLsn: started.EndLsn, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
            };
        }
        finally
        {
            fileService.ReleaseBackupFileContextRetention(backupFileContext: registered);
        }
    });

    public ValueTask<BackupFileContext> ReadBackupFileContextManifestAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch<BackupFileContext>(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return await fileService.ReadBackupFileContextManifestAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> ReadBackupFileContextVerificationAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch<BackupFileContext>(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return await fileService.ReadBackupFileContextVerificationAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> RestoreBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch<BackupFileContext>(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return backupFileContext with
        {
            TextResult = await RestoreAsync(backupDirectory: backupFileContext.Directory!, destinationDirectory: backupFileContext.DestinationDirectory!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
        };
    });

    public ValueTask<BackupFileContext> RestorePointInTimeBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch<BackupFileContext>(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return backupFileContext with
        {
            PointInTimeResult = await RestorePointInTimeAsync(backupDirectory: backupFileContext.Directory!, archivedSegmentPaths: backupFileContext.ArchivedPaths!, targetLsn: backupFileContext.TargetLsn, destinationDirectory: backupFileContext.DestinationDirectory!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
        };
    });

    public BackupFileContext WriteBackupFileContextArchive(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return fileService.WriteBackupFileContextArchive(backupFileContext: backupFileContext);
    });

    private async Task<BackupManifest> CreateBackupManifestPhysicalAsync(string databasePath, IEnumerable<string> walPaths, string destinationDirectory, bool requireCleanShutdown, LogSequenceNumber? startLsn, LogSequenceNumber? endLsn, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: databasePath);
        ArgumentNullException.ThrowIfNull(argument: walPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: destinationDirectory);
        var destination = ReadFullPath(path: destinationDirectory);

        if (ReadExistence(path: destination))
        {
            throw new IOException(message: $"Backup destination already exists: '{destination}'.");
        }

        DatabaseHeader header;

        var database = await pageDatabaseService.OpenPageDatabaseContextAsync(pageDatabaseContext: new PageDatabaseContext { Path = databasePath, OpenMode = DatabaseOpenMode.ReadOnly }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            header = database.Runtime!.Header!;
        }
        finally
        {
            await pageDatabaseService.ClosePageDatabaseContextAsync(pageDatabaseContext: database)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        if (requireCleanShutdown && !header.IsCleanShutdown)
        {
            throw new StorageResourceException(message: "Offline backup requires a cleanly closed database.", innerException: new InvalidOperationException());
        }

        var sources = new List<(string Path, string Name, string Kind)>
        {
            (ReadFullPath(path: databasePath), "database.db", "database")
        };

        var index = 0;

        foreach (var walPath in walPaths)
        {
            sources.Add(item: (ReadFullPath(path: walPath), $"wal-{index++:D6}.log", "wal"));
        }

        if (sources.Select(selector: source => source.Path)
            .Distinct(comparer: StringComparer.Ordinal)
            .Count() != sources.Count)
        {
            throw new ArgumentException(message: "Backup source files must be unique.", paramName: nameof(walPaths));
        }

        MakeDirectory(path: destination);

        try
        {
            var files = new List<BackupFile>();
            var lsns = new List<LogSequenceNumber>();

            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (fileService.ReadLengthBackupFileContext(backupFileContext: new BackupFileContext { Path = source.Path })
            .Length is null)
                {
                    throw new sql_storage_engine.Models.Exceptions.BackupFileNotFoundDependencyException(message: "A required backup file is missing.", fileName: source.Path);
                }

                var target = ReadCombinedPath(path1: destination, path2: source.Name);

                await CopyAsync(source: source.Path, destination: target, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                files.Add(item: await DescribeAsync(path: target, name: source.Name, kind: source.Kind, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

                if (source.Kind == "wal")
                {
                    lsns.AddRange(collection: await ReadLsnsAsync(path: target, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));
                }
            }

            var manifest = new BackupManifest
            {
                DatabaseId = header.DatabaseId,
                FormatVersion = header.FormatVersion,
                PageSize = header.PageSize,
                StartLsn = startLsn ?? (lsns.Count == 0 ? default : lsns.MinBy(keySelector: lsn => lsn.Value)),
                EndLsn = endLsn ?? (lsns.Count == 0 ? default : lsns.MaxBy(keySelector: lsn => lsn.Value)),
                CreatedUtc = fileService.ReadUtcNowBackupFileContext(backupFileContext: new BackupFileContext())
            .CreatedAt,
                EngineVersion = fileService.ReadEngineVersionBackupFileContext(backupFileContext: new BackupFileContext())
            .TextResult!,
                Files = files.AsReadOnly()
            };

            await fileService.WriteBackupFileContextManifestAsync(backupFileContext: new BackupFileContext { Directory = destination, Manifest = manifest }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return manifest;
        }
        catch
        {
            RemoveDirectory(path: destination);
            throw;
        }
    }

    private async Task<string> RestoreAsync(string backupDirectory, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        var verification = await VerifyAsync(backupDirectory: backupDirectory, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!verification.IsValid)
        {
            throw new StorageCorruptionException(message: string.Join(separator: ';', values: verification.Errors));
        }

        var manifest = await ReadManifestAsync(backupDirectory: backupDirectory, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var destination = ReadFullPath(path: destinationDirectory);

        if (ReadExistence(path: destination))
        {
            throw new IOException(message: $"Restore destination already exists: '{destination}'.");
        }

        MakeDirectory(path: destination);

        try
        {
            foreach (var file in manifest.Files)
            {
                await CopyAsync(source: SafeBackupPath(root: backupDirectory, name: file.Name), destination: ReadCombinedPath(path1: destination, path2: file.Name), token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            var databasePath = ReadCombinedPath(path1: destination, path2: manifest.Files.Single(predicate: file => file.Kind == "database")
            .Name);

            await VerifyDatabasePagesAsync(path: databasePath, manifest: manifest, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return databasePath;
        }
        catch
        {
            RemoveDirectory(path: destination);
            throw;
        }
    }

    private string SafeBackupPath(string root, string name)
    {
        if (name != ReadFileName(path: name))
        {
            throw new StorageFormatException(message: "Backup file name escapes its directory.");
        }

        return ReadCombinedPath(path1: ReadFullPath(path: root), path2: name);
    }

    private async Task<PointInTimeRestoreResult> RestorePointInTimeAsync(string backupDirectory, IEnumerable<string> archivedSegmentPaths, LogSequenceNumber targetLsn, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        if (targetLsn.Value == 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(targetLsn));
        }

        var manifest = await ReadManifestAsync(backupDirectory: backupDirectory, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var segments = new List<(WalSegmentHeader Header, IReadOnlyList<WalRecord> Records)>();

        foreach (var path in archivedSegmentPaths)
        {
            var archive = await fileService.ReadBackupFileContextArchiveAsync(backupFileContext: new BackupFileContext { Path = path, Manifest = manifest }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            segments.Add(item: (archive.SegmentHeader!, archive.Records!));
        }

        if (segments.Count == 0)
        {
            throw new MissingWalSegmentException(message: "No archived WAL segments were supplied.");
        }

        segments.Sort(comparison: (left, right) => left.Header.SegmentNumber.CompareTo(value: right.Header.SegmentNumber));
        var timeline = segments[0].Header.Timeline;

        for (var index = 0; index < segments.Count; index++)
        {
            if (segments[index].Header.Timeline != timeline || index > 0 && segments[index].Header.SegmentNumber != segments[index - 1].Header.SegmentNumber + 1)
            {
                throw new MissingWalSegmentException(message: "Archived WAL timeline has a missing or mismatched segment.");
            }
        }

        var selected = segments.SelectMany(selector: segment => segment.Records)
            .Where(predicate: record => record.Lsn.Value <= targetLsn.Value)
            .OrderBy(keySelector: record => record.Lsn.Value)
            .ToArray();

        if (selected.Length == 0 || selected[^1].Lsn.Value < targetLsn.Value)
        {
            throw new MissingWalSegmentException(message: "Archived WAL does not reach the requested target LSN.");
        }

        var committed = selected.Where(predicate: record => record.Type == WalRecordType.Commit)
            .Select(selector: record => record.TransactionId)
            .Distinct()
            .OrderBy(keySelector: id => id.Value)
            .ToArray();

        var databasePath = await RestoreAsync(backupDirectory: backupDirectory, destinationDirectory: destinationDirectory, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var newTimeline = checked(timeline + 1);

        await fileService.WriteBackupFileContextValueAsync(backupFileContext: new BackupFileContext { Path = ReadCombinedPath(path1: destinationDirectory, path2: "timeline.json"), ValueType = typeof(BackupTimeline), ValueSource = new BackupTimeline { DatabaseId = manifest.DatabaseId.Value, Timeline = newTimeline, ParentTimeline = timeline, ForkLsn = targetLsn.Value } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new PointInTimeRestoreResult
        {
            DatabasePath = databasePath,
            Timeline = newTimeline,
            ReplayedThrough = targetLsn,
            CommittedTransactions = committed
        };
    }

    private string ReadFullPath(string path) =>
        fileService.ReadFullPathBackupFileContext(backupFileContext: new BackupFileContext { Path = path })
            .TextResult!;

    private string ReadCombinedPath(string path1, string path2) =>
        fileService.ReadCombinedPathBackupFileContext(backupFileContext: new BackupFileContext { Directory = path1, Name = path2 })
            .TextResult!;

    private string ReadFileName(string path) =>
        fileService.ReadFileNameBackupFileContext(backupFileContext: new BackupFileContext { Path = path })
            .TextResult!;

    private bool ReadExistence(string path) =>
        fileService.ReadExistenceBackupFileContext(backupFileContext: new BackupFileContext { Path = path })
            .Exists;

    private void MakeDirectory(string path) =>
        fileService.MakeDirectoryBackupFileContext(backupFileContext: new BackupFileContext { Path = path });

    private void RemoveDirectory(string path) =>
        fileService.RemoveDirectoryBackupFileContext(backupFileContext: new BackupFileContext { Path = path });

    private ValueTask<BackupFileContext> CopyAsync(string source, string destination, CancellationToken token) =>
        fileService.CopyBackupFileContextAsync(backupFileContext: new BackupFileContext { SourcePath = source, DestinationPath = destination }, cancellationToken: token);

    private async Task<BackupFile> DescribeAsync(string path, string name, string kind, CancellationToken token) =>
        (await fileService.DescribeBackupFileContextAsync(backupFileContext: new BackupFileContext { Path = path, Name = name, Kind = kind }, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).FileResult!;

    private async Task<IReadOnlyList<LogSequenceNumber>> ReadLsnsAsync(string path, CancellationToken token) =>
        fileService.ReadBackupFileContextRecords(backupFileContext: await fileService.ReadBytesBackupFileContextAsync(backupFileContext: new BackupFileContext { Path = path }, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false))
            .Records!.Select(selector: record => record.Lsn)
            .ToArray();

    private async Task<BackupManifest> ReadManifestAsync(string backupDirectory, CancellationToken cancellationToken) =>
        (await fileService.ReadBackupFileContextManifestAsync(backupFileContext: new BackupFileContext { Directory = backupDirectory }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Manifest!;

    private async Task<BackupVerificationResult> VerifyAsync(string backupDirectory, CancellationToken cancellationToken) =>
        (await fileService.ReadBackupFileContextVerificationAsync(backupFileContext: new BackupFileContext { Directory = backupDirectory }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Verification!;

    private async Task VerifyDatabasePagesAsync(string path, BackupManifest manifest, CancellationToken token)
    {
        var database = await pageDatabaseService.OpenPageDatabaseContextAsync(pageDatabaseContext: new PageDatabaseContext { Path = path, OpenMode = DatabaseOpenMode.ReadOnly }, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var header = database.Runtime!.Header!;

            if (header.DatabaseId != manifest.DatabaseId || header.FormatVersion != manifest.FormatVersion || database.PageSize != manifest.PageSize)
            {
                throw new StorageCorruptionException(message: "Restored database identity or format differs from its manifest.");
            }

            var page = new byte[database.PageSize];

            for (ulong value = 0; value < header.NextPageId.Value; value++)
            {
                await pageDatabaseService.ReadPageDatabaseContextAsync(pageDatabaseContext: database, pageId: value, destination: page, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);

                pageDatabaseService.CheckPageDatabaseContextPage(pageDatabaseContext: database, pageId: value, source: page);
            }
        }
        finally
        {
            await pageDatabaseService.ClosePageDatabaseContextAsync(pageDatabaseContext: database)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
    }
}