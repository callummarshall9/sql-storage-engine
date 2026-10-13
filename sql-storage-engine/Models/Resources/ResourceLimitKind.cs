// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Resources;
internal enum ResourceLimitKind
{
    Transactions = 1,
    Pins,
    RowBytes,
    KeyBytes,
    ValueBytes,
    OverflowPages,
    UndoBytes,
    ScanPages
}