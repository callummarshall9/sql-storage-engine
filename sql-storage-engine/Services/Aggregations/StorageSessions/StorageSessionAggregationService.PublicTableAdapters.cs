// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Tables;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private void EnsurePublicTableCurrent(StoragePublicTableContext storagePublicTableContext) =>
        EnsureStorageIndexCurrent(storageIndexContext: new StorageIndexContext { Handle = storagePublicTableContext.Handle });

    private ValueTask<EngineResourceContext> EnterPublicTableScopeAsync(StoragePublicTableContext storagePublicTableContext, CancellationToken cancellationToken) =>
        EnterStorageIndexScopeAsync(storageIndexContext: new StorageIndexContext { Handle = storagePublicTableContext.Handle }, cancellationToken: cancellationToken, reentrant: !storagePublicTableContext.Coordinate);

    private async ValueTask FlushPublicEngineAsync(StorageEngineRuntime storageEngineRuntime, CancellationToken cancellationToken)
    {
        var resource = (EngineResourceContext)storageEngineRuntime.ResourceSource!;

        await engineResources.FlushBufferEngineResourceContextAsync(engineResourceContext: resource, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (catalog.RootPageId is { } root)
        {
            await engineResources.PublishCatalogRootEngineResourceContextAsync(engineResourceContext: resource with { PageId = root.Value }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private static string FormatPublicTableName(CatalogTableName catalogTableName) =>
        $"{catalogTableName.SchemaName}.{catalogTableName.Name}";

    private bool TryGetPublicTemporalCurrent(TableId historyTableId, out CatalogTable? currentTable)
    {
        currentTable = catalog.Definition.Tables.SingleOrDefault(predicate: table => table.SystemVersioning?.HistoryTableId == historyTableId);
        return currentTable is not null;
    }

    private SqlValue ConvertPublicTableValue(SqlType sqlType, SqlValue sqlValue) =>
        sqlValues.ConvertSqlValueRequest(sqlValueRequest: new SqlValueRequest { Input = sqlType, Other = sqlValue })
            .Result!;

    private Row ApplyPublicTableMasking(StoragePublicTableContext storagePublicTableContext, Row row, StorageReadOptions readOptions) =>
        new Row
    {
        Values = Array.AsReadOnly(array: row.Values.Select(selector: (value, position) =>
        {
            var column = storagePublicTableContext.Storage!.Definition!.Columns[position];

            return (readOptions.CanUnmaskAll || readOptions.UnmaskedColumns.Contains(item: column.Id)) ? value : sqlMasks.ApplySqlMaskContext(sqlMaskContext: new SqlMaskContext { Column = column, Value = value })
            .Result!;
        })
            .ToArray())
    };

    private async ValueTask<RowId> LowPublicTableInsertAsync(TableStorageContext tableStorageContext, Row row, CancellationToken cancellationToken) =>
        (await AddStorageSessionContextTableAsync(newStorageSessionContext: new StorageSessionContext { Table = tableStorageContext with { Binding = tableStorageContext, Row = row } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.InsertResult!.RowId!.Value;

    private async ValueTask<TableInsertResult> LowPublicTableTryInsertAsync(TableStorageContext tableStorageContext, Row row, CancellationToken cancellationToken) =>
        (await AddStorageSessionContextTableTryAsync(newStorageSessionContext: new StorageSessionContext { Table = tableStorageContext with { Binding = tableStorageContext, Row = row } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.InsertResult!;

    private IAsyncEnumerable<StoredRow> LowPublicTableScanAsync(TableStorageContext tableStorageContext, CancellationToken cancellationToken) =>
        ReadStorageSessionContextTableScan(storageSessionContext: new StorageSessionContext { Table = tableStorageContext }, cancellationToken: cancellationToken)
            .Table!.Rows!;

    private IAsyncEnumerable<StoredRow> LowPublicTableSampleAsync(TableStorageContext tableStorageContext, StorageTableSample sample, CancellationToken cancellationToken) =>
        ReadStorageSessionContextTableSample(storageSessionContext: new StorageSessionContext { Table = tableStorageContext with { Binding = tableStorageContext, PagePercentage = sample is StoragePercentTableSample percent ? percent.Percentage : 0, RequestedRows = sample is StorageRowsTableSample rows ? rows.RowCount : null, RepeatableSeed = sample.RepeatableSeed } }, cancellationToken: cancellationToken)
            .Table!.Rows!;

    private async ValueTask<TableUpdateResult> LowPublicTableUpdateAsync(TableStorageContext tableStorageContext, RowId rowId, RowUpdate update, CancellationToken cancellationToken) =>
        (await UpdateStorageSessionContextTableAsync(updatedStorageSessionContext: new StorageSessionContext { Table = tableStorageContext with { Binding = tableStorageContext, RowId = rowId, Update = update } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.UpdateResult!;

    private async ValueTask<TableUpdateResult> LowPublicTableUpdateSystemVersionedAsync(TableStorageContext tableStorageContext, RowId rowId, RowUpdate update, SqlValue periodStart, CancellationToken cancellationToken) =>
        (await UpdateStorageSessionContextTableSystemVersionedAsync(updatedStorageSessionContext: new StorageSessionContext { Table = tableStorageContext with { Binding = tableStorageContext, RowId = rowId, Update = update, PeriodStart = periodStart } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.UpdateResult!;

    private async ValueTask<TableDeleteResult> LowPublicTableDeleteAsync(TableStorageContext tableStorageContext, RowId rowId, CancellationToken cancellationToken) =>
        (await DeleteStorageSessionContextTableAsync(deletedStorageSessionContext: new StorageSessionContext { Table = tableStorageContext with { Binding = tableStorageContext, RowId = rowId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.DeleteResult!;

    private async ValueTask<TableInsertResult> LowPublicTableInsertPreparedAsync(TableStorageContext tableStorageContext, Row row, CancellationToken cancellationToken) =>
        (await AddStorageSessionContextTablePreparedAsync(newStorageSessionContext: new StorageSessionContext { Table = tableStorageContext with { Binding = tableStorageContext, Row = row } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.InsertResult!;

    private async ValueTask<StoragePublicTableContext> ExecutePublicTableTemporalAtomicAsync(StoragePublicTableContext storagePublicTableContext, CancellationToken cancellationToken)
    {
        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var lifetime = lease.Lifetime!;
        var scope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease);
        using var scopeLifetime = scope.Lifetime!;
        EngineResourceContext? journal = null;

        var statement = new StorageStatementRuntime
        {
            Active = true
        };

        try
        {
            await FlushPublicEngineAsync(storageEngineRuntime: storagePublicTableContext.Handle!.Engine!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            journal = await engineResources.CreateJournalEngineResourceContextAsync(engineResourceContext: lease, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            var nested = storagePublicTableContext with
            {
                Coordinate = false,
                FlushMutations = false,
                Handle = storagePublicTableContext.Handle with
                {
                    Statement = statement
                }
            };

            if (nested.Operation == "Update")
            {
                storagePublicTableContext.UpdateResult = await PublicTableUpdateAsync(storagePublicTableContext: nested, rowId: nested.RowId, update: nested.Update!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            else
            {
                storagePublicTableContext.DeleteResult = await PublicTableDeleteAsync(storagePublicTableContext: nested, rowId: nested.RowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            statement.Active = false;

            await engineResources.DrainEngineResourceContextAsync(engineResourceContext: scope, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

            cancellationToken.ThrowIfCancellationRequested();

            await FlushPublicEngineAsync(storageEngineRuntime: storagePublicTableContext.Handle.Engine!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await engineResources.CommitJournalEngineResourceContextAsync(engineResourceContext: journal, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return storagePublicTableContext;
        }
        catch (Exception failure)
        {
            statement.Active = false;

            if (journal is not null)
            {
                try
                {
                    await RestorePublicEngineJournalAsync(storageEngineRuntime: storagePublicTableContext.Handle!.Engine!, journal: journal, retain: false, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (Exception rollbackFailure)
                {
                    throw new AggregateException(message: "Statement failed and its durable rollback also failed.", innerExceptions: [failure, rollbackFailure]);
                }
            }

            throw;
        }
        finally
        {
            if (journal is not null)
            {
                await engineResources.CloseJournalEngineResourceContextAsync(engineResourceContext: journal, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
        }
    }
}