// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.IndexTreeResources;
internal interface IIndexRootResourceBroker
{
    ulong Read();

    ValueTask UpdateAsync(ulong rootPageId, CancellationToken cancellationToken);
}