// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Services.Processings.BackupSegments;
using sql_storage_engine.Services.Processings.WalReadResults;

namespace sql_storage_engine.Services.Orchestrations.BackupArchives;
internal sealed partial class BackupArchiveFileContextOrchestrationService : IBackupArchiveFileContextOrchestrationService
{
    private readonly WalRecordReadProcessingService records;
    private readonly WalSegmentHeaderBackupFileContextProcessingService segments;
    public BackupArchiveFileContextOrchestrationService(WalRecordReadProcessingService records, WalSegmentHeaderBackupFileContextProcessingService segments)
    {
        this.records = records;
        this.segments = segments;
    }

    internal static BackupArchiveFileContextOrchestrationService CreateBackupArchiveFileContextOrchestrationService() =>
        new BackupArchiveFileContextOrchestrationService(records: WalRecordReadProcessingService.CreateWalRecordReadProcessingService(), segments: WalSegmentHeaderBackupFileContextProcessingService.CreateWalSegmentHeaderBackupFileContextProcessingService());

    public BackupFileContext ReadBackupFileContextRecords(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateRecords(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            Records = records.ReadWalReadResult(source: backupFileContext.Bytes!)
            .Records.ToArray()
        };
    });

    public BackupFileContext ReadBackupFileContextArchive(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateRecords(backupFileContext: backupFileContext);
        var decoded = segments.ReadBackupFileContextSegment(backupFileContext: backupFileContext);

        return decoded with
        {
            Records = records.ReadWalReadResult(source: backupFileContext.Bytes.AsSpan(start: WalFormatConstants.SegmentHeaderLength))
            .Records.ToArray()
        };
    });

    public BackupFileContext WriteBackupFileContextArchive(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateArchive(backupFileContext: backupFileContext);
        var snapshot = backupFileContext.RecordSource!.ToArray();
        var encoded = records.WriteWalReadResult(walReadResult: new WalReadResult { Records = snapshot });

        var header = segments.WriteBackupFileContextSegment(backupFileContext: backupFileContext)
            .Bytes!;

        var result = new byte[checked(header.Length + encoded.Length)];
        header.CopyTo(array: result, index: 0);
        encoded.CopyTo(array: result, index: header.Length);

        return backupFileContext with
        {
            Bytes = result,
            Records = snapshot
        };
    });
}