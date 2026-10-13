// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Orchestrations.UpgradeFiles;
internal sealed partial class UpgradeFileOrchestrationService
{
    private static void ValidateUpgradeContext(UpgradeContext upgradeContext, CancellationToken cancellationToken = default)
    {
        Validate(inputs: [(upgradeContext is null, nameof(upgradeContext)), (string.IsNullOrWhiteSpace(value: upgradeContext?.DatabasePath), nameof(UpgradeContext.DatabasePath))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateUpgradeContextWrite(UpgradeContext upgradeContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(upgradeContext is null, nameof(upgradeContext)), (string.IsNullOrWhiteSpace(value: upgradeContext?.DatabasePath), nameof(UpgradeContext.DatabasePath)), (upgradeContext?.Bytes is null || upgradeContext.Bytes.Length < upgradeContext.PageSize, nameof(UpgradeContext.Bytes)), (upgradeContext?.PageSize < 64 || !Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)upgradeContext!.PageSize), nameof(UpgradeContext.PageSize))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Validate((bool Invalid, string ParameterName)[] inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "A valid upgrade context is required.", paramName: input.ParameterName);
            }
        }
    }
}