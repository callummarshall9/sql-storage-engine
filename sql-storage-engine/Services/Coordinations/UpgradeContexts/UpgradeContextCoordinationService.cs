// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Orchestrations.UpgradeFiles;
using sql_storage_engine.Services.Orchestrations.UpgradeProgress;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Coordinations.UpgradeContexts;
internal sealed partial class UpgradeContextCoordinationService : IUpgradeContextCoordinationService
{
    private readonly UpgradeFileOrchestrationService files = new UpgradeFileOrchestrationService();
    private readonly UpgradeProgressOrchestrationService checkpoints = new UpgradeProgressOrchestrationService();
    private UpgradeProgress? current;
    private readonly List<string> activity = new List<string>();
    public ValueTask<UpgradeContext> PrepareUpgradeContextAsync(UpgradeContext upgradeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUpgradeContext(upgradeContext: upgradeContext, cancellationToken: cancellationToken);

        return await files.ReadUpgradeContextAsync(upgradeContext: upgradeContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public UpgradeContext ReadUpgradeContext(UpgradeContext upgradeContext) =>
        TryCatch(operation: () =>
    {
        ValidateUpgradeContextRead(upgradeContext: upgradeContext);

        return upgradeContext with
        {
            Progress = current,
            Activity = new List<string>(collection: activity)
        };
    });

    public ValueTask ProcessUpgradeContextAsync(UpgradeContext upgradeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUpgradeContext(upgradeContext: upgradeContext, cancellationToken: cancellationToken);

        if (!upgradeContext.VerifiedBackup)
        {
            throw new StorageCorruptionException(message: "A verified backup is required before upgrade.");
        }

        var loaded = await checkpoints.ReadUpgradeContextAsync(upgradeContext: upgradeContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var progress = loaded.Progress!;

        if (loaded.CreatedProgress)
        {
            await checkpoints.WriteUpgradeContextAsync(upgradeContext: upgradeContext with { Progress = progress }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        current = progress;
        activity.Add(item: $"resume:{progress.NextStep}");

        if (progress.NextStep == 0 && progress.SourceVersion == 0)
        {
            await files.WriteUpgradeContextAsync(upgradeContext: upgradeContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            progress = progress with
            {
                NextStep = 1
            };

            current = progress;

            await checkpoints.WriteUpgradeContextAsync(upgradeContext: upgradeContext with { Progress = progress }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            activity.Add(item: $"step:0-to-{DatabaseHeaderFormat.CurrentVersion}");

            if (upgradeContext.Boundary is not null)
            {
                await upgradeContext.Boundary(arg1: 1, arg2: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
        }

        progress = progress with
        {
            Status = UpgradeStatus.Validating
        };

        current = progress;

        await checkpoints.WriteUpgradeContextAsync(upgradeContext: upgradeContext with { Progress = progress }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var valid = upgradeContext.CheckIntegrity is null || await upgradeContext.CheckIntegrity(arg1: upgradeContext.DatabasePath!, arg2: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!valid)
        {
            progress = progress with
            {
                Status = UpgradeStatus.Failed
            };

            current = progress;

            await checkpoints.WriteUpgradeContextAsync(upgradeContext: upgradeContext with { Progress = progress }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            activity.Add(item: "integrity:failed");
            throw new StorageCorruptionException(message: "Post-upgrade integrity validation failed.");
        }

        progress = progress with
        {
            Status = UpgradeStatus.Complete
        };

        current = progress;

        await checkpoints.WriteUpgradeContextAsync(upgradeContext: upgradeContext with { Progress = progress }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        activity.Add(item: $"complete:{DatabaseHeaderFormat.CurrentVersion}");
    });
}