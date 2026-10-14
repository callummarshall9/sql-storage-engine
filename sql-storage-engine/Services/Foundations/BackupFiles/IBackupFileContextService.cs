// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Foundations.BackupFiles;
internal interface IBackupFileContextService
{
    BackupFileContext ReadFullPathBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext ReadCombinedPathBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext ReadFileNameBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext ReadExistenceBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext ReadLengthBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext ReadUtcNowBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext SerializeManifestBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext DeserializeManifestBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext MakeDirectoryBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext RemoveDirectoryBackupFileContext(BackupFileContext backupFileContext);

    ValueTask<BackupFileContext> ReadBytesBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> ReadPageBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> WriteBytesBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> CopyBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> DescribeBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    BackupFileContext MatchChecksumBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext ReadEngineVersionBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext SerializeTimelineBackupFileContext(BackupFileContext backupFileContext);

    BackupFileContext DeserializeTimelineBackupFileContext(BackupFileContext backupFileContext);
}