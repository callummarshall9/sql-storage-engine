// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Processings.BackupSegments;
internal sealed partial class WalSegmentHeaderBackupFileContextProcessingService
{
    private static void ValidateSegment(BackupFileContext backupFileContext)
    {
        ArgumentNullException.ThrowIfNull(argument: backupFileContext);
        ArgumentNullException.ThrowIfNull(argument: backupFileContext.Bytes);
    }

    private static void ValidateHeader(BackupFileContext backupFileContext)
    {
        ArgumentNullException.ThrowIfNull(argument: backupFileContext);

        if (backupFileContext.SegmentHeader is null)
        {
            throw new ArgumentNullException(paramName: nameof(backupFileContext.SegmentHeader));
        }
    }
}