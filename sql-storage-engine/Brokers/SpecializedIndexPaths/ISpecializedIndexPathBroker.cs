// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Brokers.SpecializedIndexPaths;
internal interface ISpecializedIndexPathBroker
{
    void CheckJsonProperty(string value);

    void CheckNamespace(string prefix);

    void CheckXPath(string path, (string Prefix, string Uri)[] namespaces);
}