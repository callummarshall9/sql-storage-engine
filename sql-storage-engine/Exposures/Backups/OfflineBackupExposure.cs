// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Services.Managements.Backups;

namespace sql_storage_engine.Exposures.Backups;
public sealed class OfflineBackupExposure : ICompositionExposure, IPhysicalBackupExposure
{
    public const string ManifestFileName = "manifest.json";
    private readonly BackupContextManagementService service;
    public OfflineBackupExposure()
    {
        service = BackupContextManagementService.CreateBackupContextManagementService(configuration: new BackupConfiguration());
    }

    public OfflineBackupExposure(BackupConfiguration backupConfiguration)
    {
        service = BackupContextManagementService.CreateBackupContextManagementService(configuration: backupConfiguration);
    }

    public BackupServiceBinding ReadBackupServiceBinding() =>
        new BackupServiceBinding
    {
        Source = service,
        Configuration = (BackupConfiguration)service.ReadBackupFileContextBinding(backupFileContext: new BackupFileContext())
            .ValueSource!
    };

    public async Task<BackupManifest> CreateBackupManifestAsync(string databasePath, IEnumerable<string> walPaths, string destinationDirectory, CancellationToken cancellationToken = default) =>
        (await service.CreateBackupFileContextAsync(backupFileContext: new BackupFileContext { DatabasePath = databasePath, WalPaths = walPaths, DestinationDirectory = destinationDirectory }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Manifest!;

    public async Task<BackupVerificationResult> VerifyAsync(string backupDirectory, CancellationToken cancellationToken = default) =>
        (await service.ReadBackupFileContextVerificationAsync(backupFileContext: new BackupFileContext { Directory = backupDirectory }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Verification!;

    public async Task<string> RestoreAsync(string backupDirectory, string destinationDirectory, CancellationToken cancellationToken = default) =>
        (await service.RestoreBackupFileContextAsync(backupFileContext: new BackupFileContext { Directory = backupDirectory, DestinationDirectory = destinationDirectory }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).TextResult!;

    public async Task<BackupManifest> ReadManifestAsync(string backupDirectory, CancellationToken cancellationToken = default) =>
        (await service.ReadBackupFileContextManifestAsync(backupFileContext: new BackupFileContext { Directory = backupDirectory }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Manifest!;
}