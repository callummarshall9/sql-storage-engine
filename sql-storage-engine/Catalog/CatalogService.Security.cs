using sql_storage_engine.Identifiers;
using sql_storage_engine.Security;

namespace sql_storage_engine.Catalog;

public sealed partial class CatalogService
{
    internal StorageSecurityState SecurityState => _definition.Security;
    private ValueTask<(PageId RootPageId, IReadOnlyList<PageId> PageIds)> WriteDefinitionAsync(
        CatalogDefinition candidate, CancellationToken token)
    {
        candidate.Security = _definition.Security;
        return _pageChain.WriteAsync(candidate, token);
    }
    internal async ValueTask WriteSecurityAsync(StorageSecurityState state, CancellationToken token)
    {
        await _catalogMutationLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var candidate = new CatalogDefinition(Tables, Indexes, ScalarTypes, TableTypes, XmlSchemaCollections, Assemblies)
            { Security = state };
            var written = await _pageChain.WriteAsync(candidate, token).ConfigureAwait(false);
            await _bufferPool.FlushAllAsync(token).ConfigureAwait(false);
            _definition = candidate; RootPageId = written.RootPageId;
        }
        finally { _catalogMutationLock.Release(); }
    }
}
