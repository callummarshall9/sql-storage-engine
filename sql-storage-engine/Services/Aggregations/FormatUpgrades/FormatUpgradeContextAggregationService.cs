// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Services.Coordinations.UpgradeContexts;
using sql_storage_engine.Services.Coordinations.BackupFiles;

namespace sql_storage_engine.Services.Aggregations.FormatUpgrades;
internal sealed partial class FormatUpgradeContextAggregationService : IFormatUpgradeContextAggregationService
{
    private readonly UpgradeContextCoordinationService upgrades = new UpgradeContextCoordinationService();
    private readonly BackupFileContextCoordinationService backups = BackupFileContextCoordinationService.CreateBackupFileContextCoordinationService(backupConfiguration: new BackupConfiguration());
    public ValueTask ProcessUpgradeContextAsync(UpgradeContext upgradeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUpgradeContext(upgradeContext: upgradeContext, cancellationToken: cancellationToken);

        var prepared = await upgrades.PrepareUpgradeContextAsync(upgradeContext: upgradeContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var verification = await backups.ReadBackupFileContextVerificationAsync(backupFileContext: new BackupFileContext { Directory = upgradeContext.BackupDirectory }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await upgrades.ProcessUpgradeContextAsync(upgradeContext: prepared with { VerifiedBackup = verification.Verification!.IsValid }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public UpgradeContext ReadUpgradeContext(UpgradeContext upgradeContext) =>
        TryCatch(operation: () =>
    {
        ValidateUpgradeContextRead(upgradeContext: upgradeContext);
        return upgrades.ReadUpgradeContext(upgradeContext: upgradeContext);
    });
}