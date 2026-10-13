// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Aggregations.FormatUpgrades;

namespace sql_storage_engine.Exposures.Diagnostics;
public sealed class FileFormatUpgradeExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private readonly IFormatUpgradeContextAggregationService service = new FormatUpgradeContextAggregationService();
    public IReadOnlyList<string> ReadActivity() =>
        service.ReadUpgradeContext(upgradeContext: new UpgradeContext())
            .Activity!.AsReadOnly();

    public UpgradeProgress? ReadCurrentProgress() =>
        service.ReadUpgradeContext(upgradeContext: new UpgradeContext())
            .Progress;

    public Task UpgradeAsync(string databasePath, string verifiedBackupDirectory, ushort targetVersion = DatabaseHeaderFormat.CurrentVersion, Func<int, CancellationToken, ValueTask>? afterBoundary = null, Func<string, CancellationToken, Task<bool>>? integrityCheck = null, CancellationToken cancellationToken = default) =>
        service.ProcessUpgradeContextAsync(upgradeContext: new UpgradeContext { DatabasePath = databasePath, BackupDirectory = verifiedBackupDirectory, TargetVersion = targetVersion, Boundary = afterBoundary, CheckIntegrity = integrityCheck }, cancellationToken: cancellationToken)
            .AsTask();
}