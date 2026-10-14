// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Services.Foundations.IndexTreeResources;
internal interface IIndexRootResourceService
{
    ulong ReadIndexRootResource();

    ValueTask UpdateIndexRootResourceAsync(ulong rootPageId, CancellationToken cancellationToken);
}