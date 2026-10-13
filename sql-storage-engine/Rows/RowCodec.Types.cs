// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Rows;
public enum RowValueStorage : ushort
{
    Null = 0,
    Inline = 1,
    Overflow = 2
}