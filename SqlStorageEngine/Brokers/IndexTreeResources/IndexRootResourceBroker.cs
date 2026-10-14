// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Dependencies.IndexTreeResources;

namespace sql_storage_engine.Brokers.IndexTreeResources;
internal sealed class IndexRootResourceBroker : IIndexRootResourceBroker
{
    private readonly IndexRootResourceDependency dependency;
    public IndexRootResourceBroker(IndexTreeResourceConfiguration indexTreeResourceConfiguration)
    {
        dependency = new IndexRootResourceDependency(read: indexTreeResourceConfiguration.ReadRoot!, update: indexTreeResourceConfiguration.UpdateRoot!);
    }

    public ulong Read() =>
        dependency.Read();

    public ValueTask UpdateAsync(ulong rootPageId, CancellationToken cancellationToken) =>
        dependency.UpdateAsync(rootPageId: rootPageId, cancellationToken: cancellationToken);
}