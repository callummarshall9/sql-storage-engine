// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Aggregations.FormatUpgrades;
internal interface IFormatUpgradeContextAggregationService
{
    ValueTask ProcessUpgradeContextAsync(UpgradeContext upgradeContext, CancellationToken cancellationToken);

    UpgradeContext ReadUpgradeContext(UpgradeContext upgradeContext);
}