// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private static void ValidateStorageSessionContextTableOnAdd(StorageSessionContext newStorageSessionContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(newStorageSessionContext is null, nameof(newStorageSessionContext)), (newStorageSessionContext?.Table?.Row is null, "row")]);
        ValidateTableRequest(storageSessionContext: newStorageSessionContext!, cancellationToken: cancellationToken);
    }

    private static void ValidateStorageSessionContextTableTryOnAdd(StorageSessionContext newStorageSessionContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(newStorageSessionContext is null, nameof(newStorageSessionContext)), (newStorageSessionContext?.Table?.Row is null, "row")]);
        ValidateTableRequest(storageSessionContext: newStorageSessionContext!, cancellationToken: cancellationToken);
    }

    private static void ValidateStorageSessionContextTableGraphOnAdd(StorageSessionContext newStorageSessionContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(newStorageSessionContext is null, nameof(newStorageSessionContext)), (newStorageSessionContext?.Table?.Row is null, "row")]);
        ValidateTableRequest(storageSessionContext: newStorageSessionContext!, cancellationToken: cancellationToken);
    }

    private static void ValidateStorageSessionContextTablePreparedOnAdd(StorageSessionContext newStorageSessionContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(newStorageSessionContext is null, nameof(newStorageSessionContext)), (newStorageSessionContext?.Table?.Row is null, "row")]);
        ValidateTableRequest(storageSessionContext: newStorageSessionContext!, cancellationToken: cancellationToken);
    }

    private static void ValidateStorageSessionContextTableOnUpdate(StorageSessionContext updatedStorageSessionContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(updatedStorageSessionContext is null, nameof(updatedStorageSessionContext)), (updatedStorageSessionContext?.Table?.Update is null, "rowUpdate")]);
        ValidateTableRequest(storageSessionContext: updatedStorageSessionContext!, cancellationToken: cancellationToken);
    }

    private static void ValidateStorageSessionContextTableGraphOnUpdate(StorageSessionContext updatedStorageSessionContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(updatedStorageSessionContext is null, nameof(updatedStorageSessionContext)), (updatedStorageSessionContext?.Table?.Update is null, "rowUpdate")]);
        ValidateTableRequest(storageSessionContext: updatedStorageSessionContext!, cancellationToken: cancellationToken);
    }

    private static void ValidateStorageSessionContextTableSystemVersionedOnUpdate(StorageSessionContext updatedStorageSessionContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(updatedStorageSessionContext is null, nameof(updatedStorageSessionContext)), (updatedStorageSessionContext?.Table?.Update is null, "rowUpdate")]);
        ValidateTableRequest(storageSessionContext: updatedStorageSessionContext!, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: updatedStorageSessionContext!.Table!.PeriodStart);
    }

    private static void ValidateStorageSessionContextTableOnDelete(StorageSessionContext deletedStorageSessionContext, CancellationToken cancellationToken)
    {
        Validate(inputs: [(deletedStorageSessionContext is null, nameof(deletedStorageSessionContext))]);
        ValidateTableRequest(storageSessionContext: deletedStorageSessionContext!, cancellationToken: cancellationToken);
    }

    private static void ValidateTableRequest(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext);
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext.Table);
        var table = storageSessionContext.Table.Binding ?? storageSessionContext.Table;
        ArgumentNullException.ThrowIfNull(argument: table.Definition);
        ArgumentNullException.ThrowIfNull(argument: table.Schema);
        ArgumentNullException.ThrowIfNull(argument: table.HeapBinding);
        ArgumentNullException.ThrowIfNull(argument: table.Indexes);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateTableTransaction(StorageSessionContext storageSessionContext)
    {
        ValidateTableRequest(storageSessionContext: storageSessionContext, cancellationToken: default);
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext.Table!.LockingTransaction);
        Validate(inputs: [(!Enum.IsDefined(value: storageSessionContext.Table.IsolationLevel), nameof(storageSessionContext.Table.IsolationLevel))]);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: input.ParameterName);
            }
        }
    }

    private static void ValidateTableSample(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ValidateTableRequest(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        Validate(inputs: [(storageSessionContext.Table!.PagePercentage is < 0 or > 100, nameof(storageSessionContext.Table.PagePercentage)), (storageSessionContext.Table.RequestedRows < 0, nameof(storageSessionContext.Table.RequestedRows))]);
    }
}