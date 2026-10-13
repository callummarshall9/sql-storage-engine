// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Storage;
internal sealed record StorageEngineOpenHandleContext
{
    public byte GraphKind { get; init; }
    public StorageHandleContext? Handle { get; init; }
    public TableId TableId { get; init; }
    public IndexId? IndexId { get; init; }
    public StorageSessionContext? Result { get; set; }
}