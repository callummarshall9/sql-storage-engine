// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Foundations.IndexRootReferences;
internal sealed partial class IndexRootReferenceService : IIndexRootReferenceService
{
    private readonly IndexRootState state;
    public IndexRootReferenceService(IndexRootConfiguration configuration)
    {
        state = PrepareIndexRootState(configuration: configuration);
    }

    private static IndexRootState PrepareIndexRootState(IndexRootConfiguration configuration) =>
        TryCatch(operation: () =>
    {
        ValidateIndexRootConfiguration(configuration: configuration);

        return new IndexRootState
        {
            RootPageId = configuration.RootPageId
        };
    });

    public ulong ReadIndexRootPageId() =>
        TryCatch(operation: () =>
    {
        return Volatile.Read(location: ref state.RootPageId);
    });

    public ValueTask UpdateIndexRootPageIdAsync(ulong rootPageId, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateIndexRootPageIdOnUpdate(rootPageId: rootPageId, cancellationToken: cancellationToken);
        Volatile.Write(location: ref state.RootPageId, value: rootPageId);
        return ValueTask.CompletedTask;
    });
}