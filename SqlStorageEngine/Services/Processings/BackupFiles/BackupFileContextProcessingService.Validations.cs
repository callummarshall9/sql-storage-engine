// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Processings.BackupFiles;
internal sealed partial class BackupFileContextProcessingService
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

    private static void ValidateDescriptor(BackupFileContext backupFileContext, CancellationToken cancellationToken)
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: backupFileContext.Path);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: backupFileContext.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: backupFileContext.Kind);
    }
}