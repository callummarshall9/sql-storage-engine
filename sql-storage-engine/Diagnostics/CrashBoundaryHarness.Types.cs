// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Diagnostics;
public enum CrashBoundary
{
    WalAppend = 1,
    WalFlush = 2,
    PageMutation = 3,
    PageWrite = 4,
    Allocation = 5,
    CatalogRootUpdate = 6,
    Backup = 7
}