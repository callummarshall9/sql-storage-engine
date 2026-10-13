// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.UpgradeStates;
internal sealed partial class UpgradeStateService
{
    private static void ValidateUpgradeContextDecode(UpgradeContext upgradeContext) =>
        Validate(inputs: [(upgradeContext is null, nameof(upgradeContext)), (upgradeContext?.Bytes is null, nameof(UpgradeContext.Bytes))]);

    private static void ValidateUpgradeContextEncode(UpgradeContext upgradeContext)
    {
        Validate(inputs: [(upgradeContext is null, nameof(upgradeContext)), (upgradeContext?.Progress is null, nameof(UpgradeContext.Progress))]);
        ValidateUpgradeProgress(upgradeProgress: upgradeContext!.Progress!, targetVersion: upgradeContext.TargetVersion);
    }

    private static void ValidateUpgradeProgress(UpgradeProgress upgradeProgress, ushort targetVersion)
    {
        if (upgradeProgress.TargetVersion != targetVersion || upgradeProgress.SourceVersion > sql_storage_engine.Models.Pages.DatabaseHeaderFormat.CurrentVersion || upgradeProgress.NextStep is < 0 or > 1 || !Enum.IsDefined(value: upgradeProgress.Status))
        {
            throw new sql_storage_engine.Storage.StorageFormatException(message: "Upgrade progress does not describe a supported migration.");
        }
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