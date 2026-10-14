// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Brokers.UpgradeSerialization;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.UpgradeStates;
internal sealed partial class UpgradeStateService : IUpgradeStateService
{
    private readonly UpgradeSerializationBroker serialization = new UpgradeSerializationBroker();
    public UpgradeContext DecodeUpgradeContext(UpgradeContext upgradeContext) =>
        TryCatch(operation: () =>
    {
        ValidateUpgradeContextDecode(upgradeContext: upgradeContext);
        var decoded = serialization.TryDeserialize<UpgradeProgress>(bytes: upgradeContext.Bytes!);

        if (decoded.Error is not null)
        {
            throw new StorageFormatException(message: "Upgrade progress JSON is invalid.", innerException: decoded.Cause!);
        }

        var progress = decoded.Value ?? throw new StorageFormatException(message: "Upgrade progress is empty.");
        ValidateUpgradeProgress(upgradeProgress: progress, targetVersion: upgradeContext.TargetVersion);

        return upgradeContext with
        {
            Progress = progress
        };
    });

    public UpgradeContext EncodeUpgradeContext(UpgradeContext upgradeContext) =>
        TryCatch(operation: () =>
    {
        ValidateUpgradeContextEncode(upgradeContext: upgradeContext);

        return upgradeContext with
        {
            Bytes = serialization.Serialize(value: upgradeContext.Progress)
        };
    });
}