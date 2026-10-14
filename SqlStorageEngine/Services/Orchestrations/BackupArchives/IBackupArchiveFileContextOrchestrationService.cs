// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Orchestrations.BackupArchives;
internal interface IBackupArchiveFileContextOrchestrationService
{
    BackupFileContext ReadBackupFileContextRecords(BackupFileContext backupFileContext);

    BackupFileContext ReadBackupFileContextArchive(BackupFileContext backupFileContext);

    BackupFileContext WriteBackupFileContextArchive(BackupFileContext backupFileContext);
}