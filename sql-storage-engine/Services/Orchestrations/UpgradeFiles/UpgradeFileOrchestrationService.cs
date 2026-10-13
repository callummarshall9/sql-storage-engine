// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Foundations.BackupFiles;
using sql_storage_engine.Services.Foundations.PageHeaders;

namespace sql_storage_engine.Services.Orchestrations.UpgradeFiles;
internal sealed partial class UpgradeFileOrchestrationService : IUpgradeFileOrchestrationService
{
    private readonly BackupFileContextService files = BackupFileContextService.CreateBackupFileContextService(backupConfiguration: new BackupConfiguration());
    private readonly PageHeaderService headers = new PageHeaderService();
    public ValueTask<UpgradeContext> ReadUpgradeContextAsync(UpgradeContext upgradeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUpgradeContext(upgradeContext: upgradeContext, cancellationToken: cancellationToken);

        var read = await files.ReadBytesBackupFileContextAsync(backupFileContext: new BackupFileContext { Path = upgradeContext.DatabasePath }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var page = read.Bytes!;

        if (page.Length < 64)
        {
            throw new StorageFormatException(message: "Upgrade source is truncated.");
        }

        var pageSize = BinaryPrimitives.ReadInt32LittleEndian(source: page.AsSpan(start: DatabaseHeaderFormat.PayloadOffset + 28));

        if (!Enum.IsDefined(value: (PageSize)pageSize) || page.Length < pageSize)
        {
            throw new InvalidPageSizeException(pageSize: pageSize);
        }

        var sourceVersion = BinaryPrimitives.ReadUInt16LittleEndian(source: page.AsSpan(start: DatabaseHeaderFormat.PayloadOffset + 24));

        if (upgradeContext.TargetVersion != DatabaseHeaderFormat.CurrentVersion || sourceVersion > DatabaseHeaderFormat.CurrentVersion)
        {
            throw new UnsupportedDatabaseVersionException(version: sourceVersion);
        }

        if (upgradeContext.TargetVersion < sourceVersion)
        {
            throw new StorageFormatException(message: "Database format downgrade is not supported.");
        }

        return upgradeContext with
        {
            Bytes = page,
            PageSize = pageSize,
            SourceVersion = sourceVersion
        };
    });

    public ValueTask WriteUpgradeContextAsync(UpgradeContext upgradeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUpgradeContextWrite(upgradeContext: upgradeContext, cancellationToken: cancellationToken);
        var page = upgradeContext.Bytes!;
        BinaryPrimitives.WriteUInt16LittleEndian(destination: page.AsSpan(start: DatabaseHeaderFormat.PayloadOffset + 24), value: DatabaseHeaderFormat.CurrentVersion);
        headers.WriteChecksum(page: page.AsSpan(start: 0, length: upgradeContext.PageSize), pageSize: upgradeContext.PageSize);

        await files.WriteBytesBackupFileContextAsync(backupFileContext: new BackupFileContext { Path = upgradeContext.DatabasePath, Bytes = page }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}