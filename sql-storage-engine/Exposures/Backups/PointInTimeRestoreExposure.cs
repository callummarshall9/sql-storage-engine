// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Managements.Backups;

namespace sql_storage_engine.Exposures.Backups;
public sealed class PointInTimeRestoreExposure : ICompositionExposure
{
    private readonly BackupContextManagementService service;
    public PointInTimeRestoreExposure(IPhysicalBackupExposure physicalBackup)
    {
        service = (BackupContextManagementService)physicalBackup.ReadBackupServiceBinding()
            .Source!;
    }

    public async Task<PointInTimeRestoreResult> RestoreAsync(string backupDirectory, IEnumerable<string> archivedSegmentPaths, LogSequenceNumber logSequenceNumber, string destinationDirectory, CancellationToken cancellationToken = default) =>
        (await service.RestorePointInTimeBackupFileContextAsync(backupFileContext: new BackupFileContext { Directory = backupDirectory, ArchivedPaths = archivedSegmentPaths, TargetLsn = logSequenceNumber, DestinationDirectory = destinationDirectory }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PointInTimeResult!;

    public static byte[] CreateArchiveSegment(WalSegmentHeader walSegmentHeader, IEnumerable<WalRecord> records) =>
        BackupContextManagementService.CreateBackupContextManagementService(configuration: new BackupConfiguration())
            .WriteBackupFileContextArchive(backupFileContext: new BackupFileContext { SegmentHeader = walSegmentHeader, RecordSource = records })
            .Bytes!;
}