// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Orchestrations.BackupArchives;
internal sealed partial class BackupArchiveFileContextOrchestrationService
{
    private static void ValidateRecords(BackupFileContext backupFileContext)
    {
        ArgumentNullException.ThrowIfNull(argument: backupFileContext);
        ArgumentNullException.ThrowIfNull(argument: backupFileContext.Bytes);
    }

    private static void ValidateArchive(BackupFileContext backupFileContext)
    {
        ArgumentNullException.ThrowIfNull(argument: backupFileContext);
        ArgumentNullException.ThrowIfNull(argument: backupFileContext.RecordSource);
    }
}