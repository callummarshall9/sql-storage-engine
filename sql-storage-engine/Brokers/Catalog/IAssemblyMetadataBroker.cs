// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Brokers.Catalog;
internal interface IAssemblyMetadataBroker
{
    bool IsAssemblyVersion(string version);
}