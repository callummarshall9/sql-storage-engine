// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Brokers.IndexTreeResources;

namespace sql_storage_engine.Services.Foundations.IndexTreeResources;
internal sealed partial class IndexRootResourceService : IIndexRootResourceService
{
    private readonly IndexRootResourceBroker? broker;
    private readonly IndexRootContext? rootContext;
    public IndexRootResourceService(IndexTreeResourceConfiguration indexTreeResourceConfiguration)
    {
        ValidateConfiguration(indexTreeResourceConfiguration: indexTreeResourceConfiguration);
        rootContext = indexTreeResourceConfiguration.RootContext;
        if (rootContext is null)
        {
            broker = OpenIndexRootResourceBroker(indexTreeResourceConfiguration: indexTreeResourceConfiguration);
        }
    }

    private static IndexRootResourceBroker OpenIndexRootResourceBroker(IndexTreeResourceConfiguration indexTreeResourceConfiguration) =>
        TryCatch(operation: () =>
    {
        ValidateConfiguration(indexTreeResourceConfiguration: indexTreeResourceConfiguration);
        return new IndexRootResourceBroker(indexTreeResourceConfiguration: indexTreeResourceConfiguration);
    });

    public ulong ReadIndexRootResource() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        return rootContext is null ? broker!.Read() : rootContext.RootPageId;
    });

    public ValueTask UpdateIndexRootResourceAsync(ulong rootPageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateIndexRootResourceOnUpdate(rootPageId: rootPageId, cancellationToken: cancellationToken);

        if (rootContext is not null)
        {
            rootContext.RootPageId = rootPageId;
            return;
        }

        await broker!.UpdateAsync(rootPageId: rootPageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}