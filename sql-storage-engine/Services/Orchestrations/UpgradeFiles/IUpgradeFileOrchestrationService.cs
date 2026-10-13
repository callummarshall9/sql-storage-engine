// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Orchestrations.UpgradeFiles;
internal interface IUpgradeFileOrchestrationService
{
    ValueTask<UpgradeContext> ReadUpgradeContextAsync(UpgradeContext upgradeContext, CancellationToken cancellationToken);

    ValueTask WriteUpgradeContextAsync(UpgradeContext upgradeContext, CancellationToken cancellationToken);
}