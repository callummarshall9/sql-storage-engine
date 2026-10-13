// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Brokers.SpecializedIndexPaths;

namespace sql_storage_engine.Services.Foundations.SpecializedIndexPaths;
internal sealed partial class SpecializedIndexPathService(SpecializedIndexPathBroker broker) : ISpecializedIndexPathService
{
    public void CheckSpecializedIndexPathJsonProperty(string value) =>
        TryCatch(operation: () =>
    {
        ValidateText(value: value);
        broker.CheckJsonProperty(value: value);
    });

    public void CheckSpecializedIndexPathNamespace(string prefix) =>
        TryCatch(operation: () =>
    {
        ValidateText(value: prefix);
        broker.CheckNamespace(prefix: prefix);
    });

    public void CheckSpecializedIndexPathXPath(string path, (string Prefix, string Uri)[] namespaces) =>
        TryCatch(operation: () =>
    {
        ValidateXPath(path: path, namespaces: namespaces);
        broker.CheckXPath(path: path, namespaces: namespaces);
    });

    internal static SpecializedIndexPathService CreateSpecializedIndexPathService() =>
        new SpecializedIndexPathService(broker: new sql_storage_engine.Brokers.SpecializedIndexPaths.SpecializedIndexPathBroker());
}