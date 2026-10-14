// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Foundations.TransactionDirectories;
using sql_storage_engine.Brokers.Directories;
using sql_storage_engine.Services.Foundations.UpgradeStates;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.UpgradeProgress;
internal sealed partial class UpgradeProgressOrchestrationService : IUpgradeProgressOrchestrationService
{
    private readonly TransactionDirectoryService files = new TransactionDirectoryService(transactionDirectoryBroker: new TransactionDirectoryBroker());
    private readonly UpgradeStateService states = new UpgradeStateService();
    public ValueTask<UpgradeContext> ReadUpgradeContextAsync(UpgradeContext upgradeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUpgradeContext(upgradeContext: upgradeContext, cancellationToken: cancellationToken);
        var path = upgradeContext.DatabasePath + ".upgrade.json";

        if (files.ExistsTransactionFile(path: path))
        {
            var bytes = await files.ReadTransactionSnapshotBytesAsync(path: path, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return states.DecodeUpgradeContext(upgradeContext: upgradeContext with { Bytes = bytes });
        }

        if (upgradeContext.SourceVersion is not (0 or DatabaseHeaderFormat.CurrentVersion))
        {
            throw new UnsupportedDatabaseVersionException(version: upgradeContext.SourceVersion);
        }

        return upgradeContext with
        {
            CreatedProgress = true,
            Progress = new sql_storage_engine.Models.Diagnostics.UpgradeProgress
            {
                SourceVersion = upgradeContext.SourceVersion,
                TargetVersion = upgradeContext.TargetVersion,
                NextStep = 0,
                Status = UpgradeStatus.InProgress
            }
        };
    });

    public ValueTask WriteUpgradeContextAsync(UpgradeContext upgradeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUpgradeContext(upgradeContext: upgradeContext, cancellationToken: cancellationToken);
        var encoded = states.EncodeUpgradeContext(upgradeContext: upgradeContext);

        await files.PublishTransactionSnapshotBytesAsync(path: upgradeContext.DatabasePath + ".upgrade.json", bytes: encoded.Bytes!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}