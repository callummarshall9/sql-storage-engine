// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Exposures.Tables;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Services.Aggregations.StorageSessions;

namespace sql_storage_engine.Exposures.Transactions;
public sealed class TransactionalTableExposure : ICompositionExposure
{
    private readonly IStorageSessionAggregationService service;
    private readonly TableStorageContext context;
    private readonly TransactionalTableOptions options;
    public TransactionalTableExposure(ITableStorageExposure tableStorage, ILockingTransactionExposure lockingTransaction) : this(tableStorage: tableStorage, lockingTransaction: lockingTransaction, transactionalTableOptions: new TransactionalTableOptions { IsolationLevel = TransactionIsolationLevel.ReadCommitted })
    {
    }

    public TransactionalTableExposure(ITableStorageExposure tableStorage, ILockingTransactionExposure lockingTransaction, TransactionalTableOptions transactionalTableOptions)
    {
        ArgumentNullException.ThrowIfNull(argument: tableStorage);
        ArgumentNullException.ThrowIfNull(argument: lockingTransaction);
        ArgumentNullException.ThrowIfNull(argument: transactionalTableOptions);
        var binding = tableStorage.ReadTableStorageBinding();
        service = (IStorageSessionAggregationService)binding.Service!;
        options = transactionalTableOptions;
        context = service.ConfigureStorageSessionContextTableTransaction(storageSessionContext: new StorageSessionContext { Table = binding.Context!with { LockingTransaction = lockingTransaction.ReadLockingTransactionBinding(), IsolationLevel = transactionalTableOptions.IsolationLevel } })
            .Table!;
    }

    public TransactionIsolationLevel ReadIsolationLevel() =>
        options.IsolationLevel;

    public async ValueTask<(bool Found, Row? Row)> TryGetAsync(RowId rowId, CancellationToken cancellationToken = default) =>
        ReadRowResult(storageSessionContext: await service.ReadStorageSessionContextTableAsync(storageSessionContext: new StorageSessionContext { Table = context with { Binding = context, RowId = rowId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

    public async ValueTask<TableUpdateResult> UpdateAsync(RowId updatedRowId, RowUpdate updatedRowUpdate, CancellationToken cancellationToken = default) =>
        (await service.UpdateStorageSessionContextTableAsync(updatedStorageSessionContext: new StorageSessionContext { Table = context with { Binding = context, RowId = updatedRowId, Update = updatedRowUpdate } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.UpdateResult!;

    public async ValueTask<TableDeleteResult> DeleteAsync(RowId deletedRowId, CancellationToken cancellationToken = default) =>
        (await service.DeleteStorageSessionContextTableAsync(deletedStorageSessionContext: new StorageSessionContext { Table = context with { Binding = context, RowId = deletedRowId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.DeleteResult!;

    private static (bool Found, Row? Row) ReadRowResult(StorageSessionContext storageSessionContext) =>
        (storageSessionContext.Table!.Found, storageSessionContext.Table.Row);
}