// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Exposures.Retention;
using sql_storage_engine.Services.Managements.Backups;

namespace sql_storage_engine.Exposures.Backups;
public sealed class OnlineBackupExposure : ICompositionExposure
{
    private readonly BackupContextManagementService service;
    public OnlineBackupExposure(IPhysicalBackupExposure physicalBackup, IWalRetentionExposure retention, BackupConfiguration backupConfiguration)
    {
        var physicalBinding = physicalBackup.ReadBackupServiceBinding();
        var retentionBinding = retention.ReadWalRetentionServiceBinding();
        service = BackupContextManagementService.CreateBackupContextManagementService(configuration: new BackupConfiguration { TimeProviderSource = physicalBinding.Configuration!.TimeProviderSource, RetentionBinding = retentionBinding, SourceRegistrationId = retentionBinding.RegistrationId, CurrentDurableLsnSource = backupConfiguration.CurrentDurableLsnSource });
    }

    public async Task<BackupManifest> CreateBackupManifestAsync(string databasePath, IEnumerable<string> walPaths, string destinationDirectory, Func<CancellationToken, ValueTask>? afterStart = null, CancellationToken cancellationToken = default) =>
        (await service.CreateOnlineBackupFileContextAsync(backupFileContext: new BackupFileContext { DatabasePath = databasePath, WalPaths = walPaths, DestinationDirectory = destinationDirectory, AfterStart = afterStart }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Manifest!;
}