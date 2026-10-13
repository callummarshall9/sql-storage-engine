// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Processings.BackupManifests;
internal sealed partial class BackupManifestBackupFileContextProcessingService
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

    private static void ValidateManifest(BackupFileContext backupFileContext, CancellationToken cancellationToken)
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: backupFileContext.Manifest);
    }
}