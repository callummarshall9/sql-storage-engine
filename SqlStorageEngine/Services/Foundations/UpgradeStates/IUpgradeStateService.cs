// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.UpgradeStates;
internal interface IUpgradeStateService
{
    UpgradeContext DecodeUpgradeContext(UpgradeContext upgradeContext);

    ValueTask<UpgradeContext> ReadUpgradeContextAsync(UpgradeContext upgradeContext, CancellationToken cancellationToken);

    UpgradeContext EncodeUpgradeContext(UpgradeContext upgradeContext);
}