// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Foundations.BackupDurability;
internal sealed partial class BackupDurabilityFileContextService
{
    private static void ValidateBackupFileContext(BackupFileContext backupFileContext)
    {
        ArgumentNullException.ThrowIfNull(argument: backupFileContext);
    }

    private static void ValidateBackupFileContextCancellation(BackupFileContext backupFileContext, CancellationToken cancellationToken)
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        cancellationToken.ThrowIfCancellationRequested();
    }
}