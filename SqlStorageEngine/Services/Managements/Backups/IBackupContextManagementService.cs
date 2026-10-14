// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Managements.Backups;
internal interface IBackupContextManagementService
{
    BackupFileContext ReadBackupFileContextBinding(BackupFileContext backupFileContext);

    ValueTask<BackupFileContext> CreateBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> CreateOnlineBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> ReadBackupFileContextManifestAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> ReadBackupFileContextVerificationAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> RestoreBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> RestorePointInTimeBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    BackupFileContext WriteBackupFileContextArchive(BackupFileContext backupFileContext);
}