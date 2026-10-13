// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Services.Coordinations.LockingTransactions;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Transactions;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Coordinations.TableHeaps;
using sql_storage_engine.Services.Coordinations.IndexTrees;

namespace sql_storage_engine.Services.Managements.HeapIndexMutations;
internal sealed partial class HeapIndexMutationManagementService : IHeapIndexMutationManagementService
{
    private readonly TableHeapContextCoordinationService heap;
    private readonly Dictionary<ulong, IndexTreeContextCoordinationService> indexTrees;
    private readonly LockingTransactionContextCoordinationService? locking;
    private readonly HeapIndexMutationContext lockState;
    private HeapIndexMutationManagementService(TableHeapContextCoordinationService heap, Dictionary<ulong, IndexTreeContextCoordinationService> indexTrees, LockingTransactionContextCoordinationService locking, HeapIndexMutationConfiguration heapIndexMutationConfiguration)
    {
        this.heap = heap;
        this.indexTrees = indexTrees;
        this.locking = locking;
        lockState = new HeapIndexMutationContext
        {
            Transaction = heapIndexMutationConfiguration.Transaction?.State,
            TableId = heapIndexMutationConfiguration.TableId,
            IsolationLevel = heapIndexMutationConfiguration.IsolationLevel,
            HeldRows = new Dictionary<RowLockResource, LockMode>()
        };
    }

    internal static HeapIndexMutationManagementService CreateHeapIndexMutationManagementService(TableHeapBinding tableHeapBinding, TableIndexBinding[] tableIndexBindings) =>
        new(heap: (TableHeapContextCoordinationService)tableHeapBinding.Service!, indexTrees: tableIndexBindings.ToDictionary(keySelector: binding => binding.Definition!.Id.Value, elementSelector: binding => (IndexTreeContextCoordinationService)binding.Service!), locking: null!, heapIndexMutationConfiguration: new HeapIndexMutationConfiguration());

    internal static HeapIndexMutationManagementService CreateHeapIndexMutationManagementService(TableHeapBinding tableHeapBinding, TableIndexBinding[] tableIndexBindings, LockingTransactionBinding lockingTransactionBinding, TableId tableId, TransactionIsolationLevel isolationLevel) =>
        new(heap: (TableHeapContextCoordinationService)tableHeapBinding.Service!, indexTrees: tableIndexBindings.ToDictionary(keySelector: binding => binding.Definition!.Id.Value, elementSelector: binding => (IndexTreeContextCoordinationService)binding.Service!), locking: (LockingTransactionContextCoordinationService)lockingTransactionBinding.Service!, heapIndexMutationConfiguration: new HeapIndexMutationConfiguration { Transaction = lockingTransactionBinding, TableId = tableId, IsolationLevel = isolationLevel });

    public HeapIndexMutationContext RegisterHeapIndexMutationContextIndexes(HeapIndexMutationContext heapIndexMutationContext) =>
        TryCatch(operation: () =>
    {
        ValidateIndexRegistration(heapIndexMutationContext: heapIndexMutationContext);

        foreach (var binding in heapIndexMutationContext.IndexBindings!)
        {
            indexTrees[binding.Definition!.Id.Value] = (IndexTreeContextCoordinationService)binding.Service!;
        }

        return heapIndexMutationContext;
    });

    public ValueTask<HeapIndexMutationContext> PrepareHeapIndexMutationContextReadAsync(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateLockContext(heapIndexMutationContext: heapIndexMutationContext, cancellationToken: cancellationToken);

        if (locking is not null)
        {
            var resource = RowResource(heapIndexMutationContext: heapIndexMutationContext);
            var acquired = !lockState.HeldRows!.ContainsKey(key: resource);

            if (acquired)
            {
                await locking.AcquireLockingTransactionContextAsync(lockingTransactionContext: lockState.Transaction!with { Resource = resource, Mode = LockMode.Shared }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                lockState.HeldRows![resource] = LockMode.Shared;
            }

            heapIndexMutationContext.ReadLockAcquired = acquired;
        }

        return heapIndexMutationContext;
    });

    public HeapIndexMutationContext FinishHeapIndexMutationContextRead(HeapIndexMutationContext heapIndexMutationContext) =>
        TryCatch(operation: () =>
    {
        ValidateLockContext(heapIndexMutationContext: heapIndexMutationContext, cancellationToken: CancellationToken.None);

        if (locking is not null && heapIndexMutationContext.ReadLockAcquired && lockState.IsolationLevel == TransactionIsolationLevel.ReadCommitted)
        {
            var resource = RowResource(heapIndexMutationContext: heapIndexMutationContext);
            locking.ReleaseLockingTransactionContext(lockingTransactionContext: lockState.Transaction!with { Resource = resource });
            lockState.HeldRows!.Remove(key: resource);
        }

        return heapIndexMutationContext;
    });

    public ValueTask<HeapIndexMutationContext> PrepareHeapIndexMutationContextWriteAsync(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateLockContext(heapIndexMutationContext: heapIndexMutationContext, cancellationToken: cancellationToken);

        if (locking is not null)
        {
            var resource = RowResource(heapIndexMutationContext: heapIndexMutationContext);

            if (lockState.HeldRows!.TryGetValue(key: resource, value: out var current) && current == LockMode.Shared)
            {
                await locking.ConvertLockingTransactionContextAsync(lockingTransactionContext: lockState.Transaction!with { Resource = resource, Mode = LockMode.Exclusive }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            else
            {
                await locking.AcquireLockingTransactionContextAsync(lockingTransactionContext: lockState.Transaction!with { Resource = resource, Mode = LockMode.Exclusive }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            lockState.HeldRows![resource] = LockMode.Exclusive;
        }

        return heapIndexMutationContext;
    });

    private RowLockResource RowResource(HeapIndexMutationContext heapIndexMutationContext) =>
        new RowLockResource
    {
        TableId = lockState.TableId,
        RowId = heapIndexMutationContext.RowId
    };

    public ValueTask<HeapIndexMutationContext> AddHeapIndexMutationContextHeapAsync(HeapIndexMutationContext newHeapIndexMutationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateHeapIndexMutationContextHeapOnAdd(newHeapIndexMutationContext: newHeapIndexMutationContext, cancellationToken: cancellationToken);

        var result = await heap.AddTableHeapContextAsync(newTableHeapContext: new TableHeapContext { Row = newHeapIndexMutationContext.Bytes }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        newHeapIndexMutationContext.ResultRowId = result.ResultRowId;
        return newHeapIndexMutationContext;
    });

    public ValueTask<HeapIndexMutationContext> UpdateHeapIndexMutationContextHeapAsync(HeapIndexMutationContext updatedHeapIndexMutationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateHeapIndexMutationContextHeapOnUpdate(updatedHeapIndexMutationContext: updatedHeapIndexMutationContext, cancellationToken: cancellationToken);

        var result = await heap.UpdateTableHeapContextAsync(updatedTableHeapContext: new TableHeapContext { RowId = updatedHeapIndexMutationContext.RowId, Row = updatedHeapIndexMutationContext.Bytes }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        updatedHeapIndexMutationContext.UpdateResult = result.UpdateResult;
        return updatedHeapIndexMutationContext;
    });

    public ValueTask<HeapIndexMutationContext> DeleteHeapIndexMutationContextHeapAsync(HeapIndexMutationContext deletedHeapIndexMutationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateHeapIndexMutationContextHeapOnDelete(deletedHeapIndexMutationContext: deletedHeapIndexMutationContext, cancellationToken: cancellationToken);

        var result = await heap.DeleteTableHeapContextAsync(deletedTableHeapContext: new TableHeapContext { RowId = deletedHeapIndexMutationContext.RowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        deletedHeapIndexMutationContext.Result = result.Result;
        return deletedHeapIndexMutationContext;
    });

    public ValueTask<HeapIndexMutationContext> ReadHeapIndexMutationContextHeapAsync(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateContext(heapIndexMutationContext: heapIndexMutationContext, cancellationToken: cancellationToken);

        var result = await heap.ReadTableHeapContextRowAsync(tableHeapContext: new TableHeapContext { RowId = heapIndexMutationContext.RowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        heapIndexMutationContext.LookupResult = result.LookupResult;
        heapIndexMutationContext.ResultBytes = result.Row;
        return heapIndexMutationContext;
    });

    public ValueTask<HeapIndexMutationContext> CountHeapIndexMutationContextHeapAsync(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateContext(heapIndexMutationContext: heapIndexMutationContext, cancellationToken: cancellationToken);

        var result = await heap.ReadTableHeapContextLiveCountAsync(tableHeapContext: new TableHeapContext(), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        heapIndexMutationContext.LiveRowCount = result.LiveRowCount;
        return heapIndexMutationContext;
    });

    public HeapIndexMutationContext ScanHeapIndexMutationContext(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateContext(heapIndexMutationContext: heapIndexMutationContext, cancellationToken: cancellationToken);

        heapIndexMutationContext.Rows = heap.ReadTableHeapContextScan(tableHeapContext: new TableHeapContext(), cancellationToken: cancellationToken)
            .Rows;

        return heapIndexMutationContext;
    });

    public HeapIndexMutationContext SampleHeapIndexMutationContext(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateContext(heapIndexMutationContext: heapIndexMutationContext, cancellationToken: cancellationToken);

        heapIndexMutationContext.Rows = heap.ReadTableHeapContextSample(tableHeapContext: new TableHeapContext { PagePercentage = heapIndexMutationContext.PagePercentage, RepeatableSeed = heapIndexMutationContext.RepeatableSeed }, cancellationToken: cancellationToken)
            .Rows;

        return heapIndexMutationContext;
    });

    public ValueTask<HeapIndexMutationContext> AddHeapIndexMutationContextIndexAsync(HeapIndexMutationContext newHeapIndexMutationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateHeapIndexMutationContextIndexOnAdd(newHeapIndexMutationContext: newHeapIndexMutationContext, cancellationToken: cancellationToken);

        var result = await indexTrees[newHeapIndexMutationContext.Index!.Definition!.Id.Value].AddIndexTreeContextBindingAsync(newIndexTreeContext: newHeapIndexMutationContext.Index!.Context!with { Binding = newHeapIndexMutationContext.Index.Context, Key = newHeapIndexMutationContext.Key, RowId = newHeapIndexMutationContext.RowId, IncludedPayload = newHeapIndexMutationContext.IncludedPayload }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return newHeapIndexMutationContext;
    });

    public ValueTask<HeapIndexMutationContext> DeleteHeapIndexMutationContextIndexAsync(HeapIndexMutationContext deletedHeapIndexMutationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateHeapIndexMutationContextIndexOnDelete(deletedHeapIndexMutationContext: deletedHeapIndexMutationContext, cancellationToken: cancellationToken);

        var result = await indexTrees[deletedHeapIndexMutationContext.Index!.Definition!.Id.Value].DeleteIndexTreeContextBindingAsync(deletedIndexTreeContext: deletedHeapIndexMutationContext.Index!.Context!with { Binding = deletedHeapIndexMutationContext.Index.Context, Key = deletedHeapIndexMutationContext.Key, RowId = deletedHeapIndexMutationContext.RowId, IncludedPayload = deletedHeapIndexMutationContext.IncludedPayload }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        deletedHeapIndexMutationContext.Result = result.DeleteResult!.Removed;
        return deletedHeapIndexMutationContext;
    });

    public ValueTask<HeapIndexMutationContext> FindHeapIndexMutationContextIndexAsync(HeapIndexMutationContext heapIndexMutationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateIndex(heapIndexMutationContext: heapIndexMutationContext, cancellationToken: cancellationToken);

        var result = await indexTrees[heapIndexMutationContext.Index!.Definition!.Id.Value].ReadIndexTreeContextEntriesAsync(indexTreeContext: heapIndexMutationContext.Index!.Context!with { Binding = heapIndexMutationContext.Index.Context, Key = heapIndexMutationContext.Key, RowId = heapIndexMutationContext.RowId, IncludedPayload = heapIndexMutationContext.IncludedPayload }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        heapIndexMutationContext.IndexRows = result.Entries!.Select(selector: entry => entry.RowId)
            .ToArray();

        return heapIndexMutationContext;
    });
}