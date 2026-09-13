namespace sql_storage_engine;

/// <summary>Durability boundaries used by internal fault/recovery qualification.</summary>
internal enum GraphDdlStage
{
    TableCreated,
    IdentityIndexCreated,
    OutgoingIndexCreated,
    IncomingIndexCreated,
    Registered
}
