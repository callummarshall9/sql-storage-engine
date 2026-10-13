// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Diagnostics;
public enum PartialWritePattern
{
    Prefix = 1,
    Suffix = 2,
    Sector = 3,
    Random = 4
}

public enum WalDamagePolicy
{
    Complete = 1,
    TruncateIncompleteTail = 2,
    StopForCorruption = 3
}