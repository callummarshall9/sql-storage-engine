// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Services.Managements.HeapIndexMutations;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    public ValueTask<StorageSessionContext> AddStorageSessionContextTableAsync(StorageSessionContext newStorageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextTableOnAdd(newStorageSessionContext: newStorageSessionContext, cancellationToken: cancellationToken);
        var table = ResolveTableRequest(tableStorageContext: newStorageSessionContext.Table!);

        table.InsertResult = await TryPersistTableRowAsync(tableStorageContext: table, row: table.Row!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!table.InsertResult!.Inserted)
        {
            throw new sql_storage_engine.Tables.DuplicateKeyIgnoredException(message: table.InsertResult.Warnings.Single());
        }

        return new StorageSessionContext
        {
            Table = table,
            Catalog = newStorageSessionContext.Catalog,
            Binding = newStorageSessionContext.Binding
        };
    });

    public ValueTask<StorageSessionContext> AddStorageSessionContextTableTryAsync(StorageSessionContext newStorageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextTableTryOnAdd(newStorageSessionContext: newStorageSessionContext, cancellationToken: cancellationToken);
        var table = ResolveTableRequest(tableStorageContext: newStorageSessionContext.Table!);

        table.InsertResult = await TryPersistTableRowAsync(tableStorageContext: table, row: table.Row!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new StorageSessionContext
        {
            Table = table,
            Catalog = newStorageSessionContext.Catalog,
            Binding = newStorageSessionContext.Binding
        };
    });

    public ValueTask<StorageSessionContext> AddStorageSessionContextTableGraphAsync(StorageSessionContext newStorageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextTableGraphOnAdd(newStorageSessionContext: newStorageSessionContext, cancellationToken: cancellationToken);
        var table = ResolveTableRequest(tableStorageContext: newStorageSessionContext.Table!);

        table.InsertResult = await PersistTableGraphRowAsync(tableStorageContext: table, row: table.Row!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new StorageSessionContext
        {
            Table = table,
            Catalog = newStorageSessionContext.Catalog,
            Binding = newStorageSessionContext.Binding
        };
    });

    public ValueTask<StorageSessionContext> AddStorageSessionContextTablePreparedAsync(StorageSessionContext newStorageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextTablePreparedOnAdd(newStorageSessionContext: newStorageSessionContext, cancellationToken: cancellationToken);
        var table = ResolveTableRequest(tableStorageContext: newStorageSessionContext.Table!);

        table.InsertResult = await PersistPreparedTableRowAsync(tableStorageContext: table, row: table.Row!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new StorageSessionContext
        {
            Table = table,
            Catalog = newStorageSessionContext.Catalog,
            Binding = newStorageSessionContext.Binding
        };
    });

    public ValueTask<StorageSessionContext> UpdateStorageSessionContextTableAsync(StorageSessionContext updatedStorageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextTableOnUpdate(updatedStorageSessionContext: updatedStorageSessionContext, cancellationToken: cancellationToken);
        var table = ResolveTableRequest(tableStorageContext: updatedStorageSessionContext.Table!);

        table.UpdateResult = await ApplyTableRowUpdateAsync(tableStorageContext: table, rowId: table.RowId, update: table.Update!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new StorageSessionContext
        {
            Table = table,
            Catalog = updatedStorageSessionContext.Catalog,
            Binding = updatedStorageSessionContext.Binding
        };
    });

    public ValueTask<StorageSessionContext> UpdateStorageSessionContextTableGraphAsync(StorageSessionContext updatedStorageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextTableGraphOnUpdate(updatedStorageSessionContext: updatedStorageSessionContext, cancellationToken: cancellationToken);
        var table = ResolveTableRequest(tableStorageContext: updatedStorageSessionContext.Table!);

        table.UpdateResult = await ApplyTableGraphRowUpdateAsync(tableStorageContext: table, rowId: table.RowId, update: table.Update!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new StorageSessionContext
        {
            Table = table,
            Catalog = updatedStorageSessionContext.Catalog,
            Binding = updatedStorageSessionContext.Binding
        };
    });

    public ValueTask<StorageSessionContext> UpdateStorageSessionContextTableSystemVersionedAsync(StorageSessionContext updatedStorageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextTableSystemVersionedOnUpdate(updatedStorageSessionContext: updatedStorageSessionContext, cancellationToken: cancellationToken);
        var table = ResolveTableRequest(tableStorageContext: updatedStorageSessionContext.Table!);

        table.UpdateResult = await ApplyTableVersionedRowUpdateAsync(tableStorageContext: table, rowId: table.RowId, update: table.Update!, periodStart: table.PeriodStart!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new StorageSessionContext
        {
            Table = table,
            Catalog = updatedStorageSessionContext.Catalog,
            Binding = updatedStorageSessionContext.Binding
        };
    });

    public ValueTask<StorageSessionContext> DeleteStorageSessionContextTableAsync(StorageSessionContext deletedStorageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageSessionContextTableOnDelete(deletedStorageSessionContext: deletedStorageSessionContext, cancellationToken: cancellationToken);
        var table = ResolveTableRequest(tableStorageContext: deletedStorageSessionContext.Table!);

        table.DeleteResult = await RemoveLogicalTableRowAsync(tableStorageContext: table, rowId: table.RowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new StorageSessionContext
        {
            Table = table,
            Catalog = deletedStorageSessionContext.Catalog,
            Binding = deletedStorageSessionContext.Binding
        };
    });

    public ValueTask<StorageSessionContext> ReadStorageSessionContextTableAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateTableRequest(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var table = ResolveTableRequest(tableStorageContext: storageSessionContext.Table!);

        var result = await ReadLogicalTableRowAsync(tableStorageContext: table, rowId: table.RowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        table.Found = result.Found;
        table.Row = result.Row;

        return new StorageSessionContext
        {
            Table = table,
            Catalog = storageSessionContext.Catalog,
            Binding = storageSessionContext.Binding
        };
    });

    public StorageSessionContext ReadStorageSessionContextTableScan(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateTableRequest(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var table = ResolveTableRequest(tableStorageContext: storageSessionContext.Table!);
        table.Rows = ScanTableRowsAsync(tableStorageContext: table, cancellationToken: cancellationToken);

        return new StorageSessionContext
        {
            Table = table,
            Catalog = storageSessionContext.Catalog,
            Binding = storageSessionContext.Binding
        };
    });

    public StorageSessionContext ReadStorageSessionContextTableSample(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateTableSample(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var table = ResolveTableRequest(tableStorageContext: storageSessionContext.Table!);
        table.Rows = SampleTableRowsAsync(tableStorageContext: table, cancellationToken: cancellationToken);

        return new StorageSessionContext
        {
            Table = table,
            Catalog = storageSessionContext.Catalog,
            Binding = storageSessionContext.Binding
        };
    });

    private TableStorageContext ResolveTableRequest(TableStorageContext tableStorageContext)
    {
        var bound = tableStorageContext;

        while (bound.Binding is not null)
        {
            bound = bound.Binding;
        }

        var context = tableStorageContext with
        {
            Definition = bound.Definition,
            Schema = bound.Schema,
            HeapBinding = bound.HeapBinding,
            Indexes = bound.Indexes,
            MutationId = tableStorageContext.MutationId == 0 ? bound.MutationId : tableStorageContext.MutationId
        };

        if (heapIndexMutations.TryGetValue(key: context.MutationId, value: out var mutations))
        {
            mutations.RegisterHeapIndexMutationContextIndexes(heapIndexMutationContext: new HeapIndexMutationContext { IndexBindings = context.Indexes! });
        }

        return context;
    }

    private TableStorageContext BindTableMutationContext(TableStorageContext tableStorageContext)
    {
        var context = tableStorageContext with
        {
            MutationId = checked((ulong)Interlocked.Increment(location: ref tableMutationSequence))
        };

        heapIndexMutations[context.MutationId] = HeapIndexMutationManagementService.CreateHeapIndexMutationManagementService(tableHeapBinding: context.HeapBinding!, tableIndexBindings: context.Indexes!);
        return context;
    }

    private long tableMutationSequence;
    public StorageSessionContext ConfigureStorageSessionContextTableTransaction(StorageSessionContext storageSessionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTableTransaction(storageSessionContext: storageSessionContext);
        var table = ResolveTableRequest(tableStorageContext: storageSessionContext.Table!);

        table = table with
        {
            Binding = storageSessionContext.Table!.Binding ?? storageSessionContext.Table,
            MutationId = checked((ulong)Interlocked.Increment(location: ref tableMutationSequence))
        };

        heapIndexMutations[table.MutationId] = HeapIndexMutationManagementService.CreateHeapIndexMutationManagementService(tableHeapBinding: table.HeapBinding!, tableIndexBindings: table.Indexes!, lockingTransactionBinding: table.LockingTransaction!, tableId: table.Definition!.Id, isolationLevel: table.IsolationLevel);

        return new StorageSessionContext
        {
            Table = table,
            Binding = new StorageSessionBinding
            {
                Service = this
            }
        };
    });

    public StorageSessionContext ReadStorageSessionContextTableDefinition(StorageSessionContext storageSessionContext) =>
        TryCatch(operation: () =>
    {
        ValidateTableRequest(storageSessionContext: storageSessionContext, cancellationToken: default);

        return new StorageSessionContext
        {
            Table = ResolveTableRequest(tableStorageContext: storageSessionContext.Table!),
            Binding = storageSessionContext.Binding,
            Catalog = storageSessionContext.Catalog
        };
    });
}