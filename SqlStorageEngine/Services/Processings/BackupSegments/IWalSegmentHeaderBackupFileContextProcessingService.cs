// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Processings.BackupSegments;
internal interface IWalSegmentHeaderBackupFileContextProcessingService
{
    BackupFileContext ReadBackupFileContextSegment(BackupFileContext backupFileContext);

    BackupFileContext WriteBackupFileContextSegment(BackupFileContext backupFileContext);
}