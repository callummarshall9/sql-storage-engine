// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Identifiers;
internal enum IdentifierDisplayKind
{
    Database = 1,
    Page,
    Table,
    Index,
    Transaction,
    LogSequence,
    Slot,
    Generation,
    Row,
    Column
}