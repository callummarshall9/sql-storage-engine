// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Services.Foundations.IndexRootReferences;
internal interface IIndexRootReferenceService
{
    ulong ReadIndexRootPageId();

    ValueTask UpdateIndexRootPageIdAsync(ulong rootPageId, CancellationToken cancellationToken);
}