// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Coordinations.UpgradeContexts;
internal sealed partial class UpgradeContextCoordinationService
{
    private static void ValidateUpgradeContext(UpgradeContext upgradeContext, CancellationToken cancellationToken = default)
    {
        Validate(inputs: [(upgradeContext is null, nameof(upgradeContext)), (string.IsNullOrWhiteSpace(value: upgradeContext?.DatabasePath), nameof(UpgradeContext.DatabasePath)), (string.IsNullOrWhiteSpace(value: upgradeContext?.BackupDirectory), nameof(UpgradeContext.BackupDirectory))]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateUpgradeContextRead(UpgradeContext upgradeContext) =>
        Validate(inputs: [(upgradeContext is null, nameof(upgradeContext))]);

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