// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Processings.BackupManifests;
internal interface IBackupManifestBackupFileContextProcessingService
{
    ValueTask<BackupFileContext> ReadBackupFileContextManifestAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);

    ValueTask<BackupFileContext> WriteBackupFileContextManifestAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);
}