// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Services.Foundations.SpecializedIndexPaths;
internal interface ISpecializedIndexPathService
{
    void CheckSpecializedIndexPathJsonProperty(string value);

    void CheckSpecializedIndexPathNamespace(string prefix);

    void CheckSpecializedIndexPathXPath(string path, (string Prefix, string Uri)[] namespaces);
}