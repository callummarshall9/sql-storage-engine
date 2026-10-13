// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Dependencies.SpecializedIndexPaths;

namespace sql_storage_engine.Brokers.SpecializedIndexPaths;
internal sealed class SpecializedIndexPathBroker : ISpecializedIndexPathBroker
{
    public void CheckJsonProperty(string value)
    {
        using var dependency = new SpecializedIndexPathDependency();
        dependency.CheckJsonProperty(value: value);
    }

    public void CheckNamespace(string prefix)
    {
        using var dependency = new SpecializedIndexPathDependency();
        dependency.CheckNamespace(prefix: prefix);
    }

    public void CheckXPath(string path, (string Prefix, string Uri)[] namespaces)
    {
        using var dependency = new SpecializedIndexPathDependency();
        dependency.CheckXPath(path: path, namespaces: namespaces);
    }
}