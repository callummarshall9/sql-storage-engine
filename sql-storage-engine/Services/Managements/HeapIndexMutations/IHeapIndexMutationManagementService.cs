// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Tables;

namespace sql_storage_engine.Services.Managements.HeapIndexMutations;
internal interface IHeapIndexMutationManagementService
{
    ValueTask<HeapIndexMutationContext> AddHeapIndexMutationContextHeapAsync(HeapIndexMutationContext newHeapIndexMutationContext, CancellationToken cancellationToken);

    ValueTask<HeapIndexMutationContext> UpdateHeapIndexMutationContextHeapAsync(HeapIndexMutationContext updatedHeapIndexMutationContext, CancellationToken cancellationToken);

    ValueTask<HeapIndexMutationContext> DeleteHeapIndexMutationContextHeapAsync(HeapIndexMutationContext deletedHeapIndexMutationContext, CancellationToken cancellationToken);

    ValueTask<HeapIndexMutationContext> ReadHeapIndexMutationContextHeapAsync(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken);

    ValueTask<HeapIndexMutationContext> CountHeapIndexMutationContextHeapAsync(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken);

    HeapIndexMutationContext ScanHeapIndexMutationContext(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken);

    HeapIndexMutationContext SampleHeapIndexMutationContext(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken);

    ValueTask<HeapIndexMutationContext> AddHeapIndexMutationContextIndexAsync(HeapIndexMutationContext newHeapIndexMutationContext, CancellationToken cancellationToken);

    ValueTask<HeapIndexMutationContext> DeleteHeapIndexMutationContextIndexAsync(HeapIndexMutationContext deletedHeapIndexMutationContext, CancellationToken cancellationToken);

    ValueTask<HeapIndexMutationContext> FindHeapIndexMutationContextIndexAsync(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken);

    ValueTask<HeapIndexMutationContext> PrepareHeapIndexMutationContextReadAsync(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken);

    HeapIndexMutationContext FinishHeapIndexMutationContextRead(HeapIndexMutationContext heapIndexMutationContext);

    ValueTask<HeapIndexMutationContext> PrepareHeapIndexMutationContextWriteAsync(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken);

    HeapIndexMutationContext RegisterHeapIndexMutationContextIndexes(HeapIndexMutationContext heapIndexMutationContext);
}