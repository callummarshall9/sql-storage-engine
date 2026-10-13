// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Diagnostics;
public enum UpgradeStatus
{
    InProgress = 1,
    Validating = 2,
    Complete = 3,
    Failed = 4
}