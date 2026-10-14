// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Coordinations.BackupFiles;
internal interface IBackupFileContextCoordinationService
{
    BackupFileContext ReadFullPathBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext ReadCombinedPathBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext ReadFileNameBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext ReadExistenceBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext ReadLengthBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext ReadUtcNowBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext ReadEngineVersionBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext MatchChecksumBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext MakeDirectoryBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext RemoveDirectoryBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext SerializeTimelineBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext DeserializeTimelineBackupFileContext(BackupFileContext backupFileContext);

    ValueTask<BackupFileContext> ReadBytesBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> ReadPageBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> WriteBytesBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> CopyBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> DescribeBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> ReadBackupFileContextManifestAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> WriteBackupFileContextManifestAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> ReadBackupFileContextVerificationAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    BackupFileContext ReadBackupFileContextRecords(BackupFileContext backupFileContext);

    BackupFileContext WriteBackupFileContextArchive(BackupFileContext backupFileContext);

    BackupFileContext RegisterBackupFileContextRetention(BackupFileContext backupFileContext);

    BackupFileContext ReleaseBackupFileContextRetention(BackupFileContext backupFileContext);

    ValueTask<BackupFileContext> StartBackupFileContextCopyAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> ReadBackupFileContextArchiveAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> ReadBackupFileContextTimelineAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> WriteBackupFileContextTimelineAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);
}