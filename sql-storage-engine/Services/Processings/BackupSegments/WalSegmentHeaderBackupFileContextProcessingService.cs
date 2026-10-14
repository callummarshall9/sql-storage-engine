// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Backup;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Services.Foundations.WalSegmentHeaders;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.BackupSegments;
internal sealed partial class WalSegmentHeaderBackupFileContextProcessingService : IWalSegmentHeaderBackupFileContextProcessingService
{
    private readonly WalSegmentHeaderService headers;
    public WalSegmentHeaderBackupFileContextProcessingService(WalSegmentHeaderService headers)
    {
        this.headers = headers;
    }

    internal static WalSegmentHeaderBackupFileContextProcessingService CreateWalSegmentHeaderBackupFileContextProcessingService() =>
        new WalSegmentHeaderBackupFileContextProcessingService(headers: new WalSegmentHeaderService());

    public BackupFileContext ReadBackupFileContextSegment(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateSegment(backupFileContext: backupFileContext);

        if (backupFileContext.Bytes!.Length < WalFormatConstants.SegmentHeaderLength)
        {
            throw new StorageFormatException(message: "Archived WAL segment is truncated.");
        }

        var header = headers.ReadWalSegmentHeader(source: backupFileContext.Bytes);

        if (backupFileContext.Manifest is not null && header.DatabaseId != backupFileContext.Manifest.DatabaseId)
        {
            throw new WrongDatabaseWalException(message: "Archived WAL belongs to a different database.");
        }

        return backupFileContext with
        {
            SegmentHeader = header
        };
    });

    public BackupFileContext WriteBackupFileContextSegment(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateHeader(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            Bytes = headers.WriteWalSegmentHeader(walSegmentHeader: backupFileContext.SegmentHeader!)
        };
    });
}