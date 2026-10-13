// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.UpgradeStates;
internal interface IUpgradeStateService
{
    UpgradeContext DecodeUpgradeContext(UpgradeContext upgradeContext);

    UpgradeContext EncodeUpgradeContext(UpgradeContext upgradeContext);
}