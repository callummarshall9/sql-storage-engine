// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Orchestrations.BackupRetentions;
internal interface IBackupRetentionFileContextOrchestrationService
{
    BackupFileContext RegisterBackupFileContextRetention(BackupFileContext backupFileContext);

    BackupFileContext ReleaseBackupFileContextRetention(BackupFileContext backupFileContext);

    ValueTask<BackupFileContext> StartBackupFileContextCopyAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);
}