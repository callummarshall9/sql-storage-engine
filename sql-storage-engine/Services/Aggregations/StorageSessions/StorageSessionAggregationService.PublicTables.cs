// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Tables;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private async ValueTask<RowId> PublicTableInsertAsync(StoragePublicTableContext storagePublicTableContext, Row row, CancellationToken cancellationToken = default)
    {
        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var scopeLease = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);
        PublicTableRejectHistoryMutation(storagePublicTableContext: storagePublicTableContext);
        PublicTableRejectGraphMutation(storagePublicTableContext: storagePublicTableContext);

        var id = await LowPublicTableInsertAsync(tableStorageContext: storagePublicTableContext.Storage!, row: row, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (storagePublicTableContext.FlushMutations)
        {
            await FlushPublicEngineAsync(storageEngineRuntime: storagePublicTableContext.Handle!.Engine!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return id;
    }

    private async ValueTask<TableInsertResult> PublicTableTryInsertAsync(StoragePublicTableContext storagePublicTableContext, Row row, CancellationToken cancellationToken = default)
    {
        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var scopeLease = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);
        PublicTableRejectHistoryMutation(storagePublicTableContext: storagePublicTableContext);
        PublicTableRejectGraphMutation(storagePublicTableContext: storagePublicTableContext);

        var result = await LowPublicTableTryInsertAsync(tableStorageContext: storagePublicTableContext.Storage!, row: row, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (storagePublicTableContext.FlushMutations)
        {
            await FlushPublicEngineAsync(storageEngineRuntime: storagePublicTableContext.Handle!.Engine!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return result;
    }

    private async ValueTask<StoredRow?> PublicTableGetAsync(StoragePublicTableContext storagePublicTableContext, RowId rowId, CancellationToken cancellationToken = default)
    {
        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var scopeLease = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        var result = await ReadPublicTableRowAsync(tableStorageContext: storagePublicTableContext.Storage!, rowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return result.Found ? new StoredRow
        {
            RowId = rowId,
            Row = result.Row!
        }

        : null;
    }

    private async ValueTask<StoredRow?> PublicTableGetAsync(StoragePublicTableContext storagePublicTableContext, RowId rowId, StorageReadOptions readOptions, CancellationToken cancellationToken = default)
    {
        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var scopeLease = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);
        ArgumentNullException.ThrowIfNull(argument: readOptions);

        var result = await ReadPublicTableRowAsync(tableStorageContext: storagePublicTableContext.Storage!, rowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return result.Found ? new StoredRow
        {
            RowId = rowId,
            Row = ApplyPublicTableMasking(storagePublicTableContext: storagePublicTableContext, row: result.Row!, readOptions: readOptions)
        }

        : null;
    }

    private async IAsyncEnumerable<StoredRow> PublicTableScanAsyncCore(StoragePublicTableContext storagePublicTableContext, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var scopeLease = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        await foreach (var stored in LowPublicTableScanAsync(tableStorageContext: storagePublicTableContext.Storage!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return stored;
        }
    }

    private async IAsyncEnumerable<StoredRow> PublicTableScanAsyncCore(StoragePublicTableContext storagePublicTableContext, StorageReadOptions readOptions, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var scopeLease = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);
        ArgumentNullException.ThrowIfNull(argument: readOptions);

        await foreach (var stored in LowPublicTableScanAsync(tableStorageContext: storagePublicTableContext.Storage!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return stored with
            {
                Row = ApplyPublicTableMasking(storagePublicTableContext: storagePublicTableContext, row: stored.Row, readOptions: readOptions)
            };
        }
    }

    private async IAsyncEnumerable<StoredRow> PublicTableSampleAsyncCore(StoragePublicTableContext storagePublicTableContext, StorageTableSample sample, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var scopeLease = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);
        ArgumentNullException.ThrowIfNull(argument: sample);

        await foreach (var stored in LowPublicTableSampleAsync(tableStorageContext: storagePublicTableContext.Storage!, sample: sample, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return stored;
        }
    }

    private async IAsyncEnumerable<StoredRow> PublicTableSampleAsyncCore(StoragePublicTableContext storagePublicTableContext, StorageTableSample sample, StorageReadOptions readOptions, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var scopeLease = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);
        ArgumentNullException.ThrowIfNull(argument: sample);
        ArgumentNullException.ThrowIfNull(argument: readOptions);

        await foreach (var stored in LowPublicTableSampleAsync(tableStorageContext: storagePublicTableContext.Storage!, sample: sample, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return stored with
            {
                Row = ApplyPublicTableMasking(storagePublicTableContext: storagePublicTableContext, row: stored.Row, readOptions: readOptions)
            };
        }
    }

    private async IAsyncEnumerable<StorageTemporalRow> PublicTableTemporalScanAsyncCore(StoragePublicTableContext storagePublicTableContext, StorageTemporalQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var scopeLease = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);
        ArgumentNullException.ThrowIfNull(argument: query);

        await foreach (var stored in PublicTableTemporalRowsAsync(storagePublicTableContext: storagePublicTableContext, query: query, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return stored;
        }
    }

    private async IAsyncEnumerable<StorageTemporalRow> PublicTableTemporalScanAsyncCore(StoragePublicTableContext storagePublicTableContext, StorageTemporalQuery query, StorageReadOptions readOptions, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var scopeLease = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);
        ArgumentNullException.ThrowIfNull(argument: query);
        ArgumentNullException.ThrowIfNull(argument: readOptions);

        await foreach (var stored in PublicTableTemporalRowsAsync(storagePublicTableContext: storagePublicTableContext, query: query, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return stored with
            {
                Row = ApplyPublicTableMasking(storagePublicTableContext: storagePublicTableContext, row: stored.Row, readOptions: readOptions)
            };
        }
    }

    private async ValueTask<TableUpdateResult> PublicTableUpdateAsync(StoragePublicTableContext storagePublicTableContext, RowId rowId, RowUpdate update, CancellationToken cancellationToken = default)
    {
        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        if (storagePublicTableContext.Coordinate && storagePublicTableContext.Storage!.Definition!.SystemVersioning is not null)
        {
            return (await ExecutePublicTableTemporalAtomicAsync(storagePublicTableContext: storagePublicTableContext with { RowId = rowId, Update = update, Operation = "Update" }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).UpdateResult!;
        }

        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var scopeLease = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);
        PublicTableRejectHistoryMutation(storagePublicTableContext: storagePublicTableContext);
        PublicTableRejectGraphMutation(storagePublicTableContext: storagePublicTableContext);

        var result = storagePublicTableContext.Storage!.Definition!.SystemVersioning is null ? await LowPublicTableUpdateAsync(tableStorageContext: storagePublicTableContext.Storage!, rowId: rowId, update: update, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) : await PublicTableUpdateTemporalAsync(storagePublicTableContext: storagePublicTableContext, rowId: rowId, update: update, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (storagePublicTableContext.FlushMutations)
        {
            await FlushPublicEngineAsync(storageEngineRuntime: storagePublicTableContext.Handle!.Engine!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return result;
    }

    private async ValueTask<TableDeleteResult> PublicTableDeleteAsync(StoragePublicTableContext storagePublicTableContext, RowId rowId, CancellationToken cancellationToken = default)
    {
        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);

        if (storagePublicTableContext.Coordinate && storagePublicTableContext.Storage!.Definition!.SystemVersioning is not null)
        {
            return (await ExecutePublicTableTemporalAtomicAsync(storagePublicTableContext: storagePublicTableContext with { RowId = rowId, Operation = "Delete" }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).DeleteResult!;
        }

        var lease = await EnterPublicTableScopeAsync(storagePublicTableContext: storagePublicTableContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var scopeLease = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        EnsurePublicTableCurrent(storagePublicTableContext: storagePublicTableContext);
        PublicTableRejectHistoryMutation(storagePublicTableContext: storagePublicTableContext);
        PublicTableRejectGraphMutation(storagePublicTableContext: storagePublicTableContext);

        var result = storagePublicTableContext.Storage!.Definition!.SystemVersioning is null ? await LowPublicTableDeleteAsync(tableStorageContext: storagePublicTableContext.Storage!, rowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) : await PublicTableDeleteTemporalAsync(storagePublicTableContext: storagePublicTableContext, rowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (storagePublicTableContext.FlushMutations)
        {
            await FlushPublicEngineAsync(storageEngineRuntime: storagePublicTableContext.Handle!.Engine!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return result;
    }

    private async IAsyncEnumerable<StorageTemporalRow> PublicTableTemporalRowsAsync(StoragePublicTableContext storagePublicTableContext, StorageTemporalQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var temporal = storagePublicTableContext.Storage!.Definition!.SystemVersioning ?? throw new InvalidOperationException($"Table {FormatPublicTableName(catalogTableName: storagePublicTableContext.Storage!.Definition!.QualifiedName)} is not system-versioned.");

        var history = await ReadCatalogTableStorageContextAsync(tableId: temporal.HistoryTableId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await foreach (var row in LowPublicTableScanAsync(tableStorageContext: history, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            if (PublicTableMatchesTemporal(storagePublicTableContext: storagePublicTableContext, row: row.Row, temporal: temporal, query: query))
            {
                yield return new StorageTemporalRow
                {
                    SourceTableId = temporal.HistoryTableId,
                    RowId = row.RowId,
                    Row = row.Row
                };
            }
        }

        await foreach (var row in LowPublicTableScanAsync(tableStorageContext: storagePublicTableContext.Storage!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            if (PublicTableMatchesTemporal(storagePublicTableContext: storagePublicTableContext, row: row.Row, temporal: temporal, query: query))
            {
                yield return new StorageTemporalRow
                {
                    SourceTableId = storagePublicTableContext.Storage!.Definition!.Id,
                    RowId = row.RowId,
                    Row = row.Row
                };
            }
        }
    }

    private async ValueTask<TableUpdateResult> PublicTableUpdateTemporalAsync(StoragePublicTableContext storagePublicTableContext, RowId rowId, RowUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: update);

        var old = await ReadPublicTableRowAsync(tableStorageContext: storagePublicTableContext.Storage!, rowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!old.Found)
        {
            return new TableUpdateResult
            {
                Updated = false,
                PreviousRowId = rowId,
                CurrentRowId = rowId,
                Relocated = false
            };
        }

        var(history, archived, timestamp) = await PublicTableArchiveAsync(storagePublicTableContext: storagePublicTableContext, oldRow: old.Row!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var result = await LowPublicTableUpdateSystemVersionedAsync(tableStorageContext: storagePublicTableContext.Storage!, rowId: rowId, update: update, periodStart: timestamp, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (!result.Updated)
            {
                await LowPublicTableDeleteAsync(tableStorageContext: history, rowId: archived, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            return result;
        }
        catch
        {
            await LowPublicTableDeleteAsync(tableStorageContext: history, rowId: archived, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

            throw;
        }
    }

    private async ValueTask<TableDeleteResult> PublicTableDeleteTemporalAsync(StoragePublicTableContext storagePublicTableContext, RowId rowId, CancellationToken cancellationToken)
    {
        var old = await ReadPublicTableRowAsync(tableStorageContext: storagePublicTableContext.Storage!, rowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!old.Found)
        {
            return new TableDeleteResult
            {
                Deleted = false,
                DeferredCleanupPageIds = []
            };
        }

        var(history, archived, _) = await PublicTableArchiveAsync(storagePublicTableContext: storagePublicTableContext, oldRow: old.Row!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var result = await LowPublicTableDeleteAsync(tableStorageContext: storagePublicTableContext.Storage!, rowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (!result.Deleted)
            {
                await LowPublicTableDeleteAsync(tableStorageContext: history, rowId: archived, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            return result;
        }
        catch
        {
            await LowPublicTableDeleteAsync(tableStorageContext: history, rowId: archived, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

            throw;
        }
    }

    private async ValueTask<(TableStorageContext History, RowId Archived, SqlValue Timestamp)> PublicTableArchiveAsync(StoragePublicTableContext storagePublicTableContext, Row oldRow, CancellationToken cancellationToken)
    {
        var temporal = storagePublicTableContext.Storage!.Definition!.SystemVersioning!;

        var history = await ReadCatalogTableStorageContextAsync(tableId: temporal.HistoryTableId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var endPosition = PublicTableColumnPosition(storagePublicTableContext: storagePublicTableContext, columnId: temporal.PeriodEndColumnId);
        var startColumn = storagePublicTableContext.Storage!.Definition!.Columns[PublicTableColumnPosition(storagePublicTableContext: storagePublicTableContext, columnId: temporal.PeriodStartColumnId)];

        var timestamp = ConvertPublicTableValue(sqlType: startColumn.Type, sqlValue: await AllocateTableGeneratedValueAsync(tableStorageContext: storagePublicTableContext.Storage!, kind: CatalogGeneratedAlwaysKind.RowStart, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

        var values = oldRow.Values.ToArray();

        if (((DateTimeSqlValue)timestamp).Value < ((DateTimeSqlValue)values[PublicTableColumnPosition(storagePublicTableContext: storagePublicTableContext, columnId: temporal.PeriodStartColumnId)]).Value)
        {
            throw new InvalidOperationException("The temporal clock moved before the current row's period start.");
        }

        values[endPosition] = timestamp;

        var inserted = await LowPublicTableInsertPreparedAsync(tableStorageContext: history, row: new Row { Values = Array.AsReadOnly(array: values) }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!inserted.Inserted)
        {
            throw new InvalidOperationException("A temporal history insert was unexpectedly ignored.");
        }

        return (history, inserted.RowId!.Value, timestamp);
    }

    private bool PublicTableMatchesTemporal(StoragePublicTableContext storagePublicTableContext, Row row, CatalogSystemVersioning temporal, StorageTemporalQuery query)
    {
        var start = ((DateTimeSqlValue)row.Values[PublicTableColumnPosition(storagePublicTableContext: storagePublicTableContext, columnId: temporal.PeriodStartColumnId)]).Value;
        var end = ((DateTimeSqlValue)row.Values[PublicTableColumnPosition(storagePublicTableContext: storagePublicTableContext, columnId: temporal.PeriodEndColumnId)]).Value;

        return query switch
        {
            StorageTemporalAsOf asOf => start <= asOf.Instant && end > asOf.Instant,
            StorageTemporalFromTo range => start < range.End && end > range.Start,
            StorageTemporalBetweenAnd range => start <= range.End && end > range.Start,
            StorageTemporalContainedIn range => start >= range.Start && end <= range.End,
            StorageTemporalAll => true,
            _ => throw new ArgumentOutOfRangeException(nameof(query), query, "Unknown temporal query type.")};
    }

    private int PublicTableColumnPosition(StoragePublicTableContext storagePublicTableContext, ColumnId columnId) =>
        storagePublicTableContext.Storage!.Definition!.Columns.Select(selector: (column, position) => (column, position))
            .Single(predicate: item => item.column.Id == columnId)
            .position;

    private void PublicTableRejectHistoryMutation(StoragePublicTableContext storagePublicTableContext)
    {
        if (TryGetPublicTemporalCurrent(historyTableId: storagePublicTableContext.Storage!.Definition!.Id, currentTable: out var current))
        {
            throw new InvalidOperationException($"Temporal history table {FormatPublicTableName(catalogTableName: storagePublicTableContext.Storage!.Definition!.QualifiedName)} is maintained by {FormatPublicTableName(catalogTableName: current!.QualifiedName)}.");
        }
    }

    private void PublicTableRejectGraphMutation(StoragePublicTableContext storagePublicTableContext)
    {
        if (storagePublicTableContext.Storage!.Definition!.Graph is not null)
        {
            throw new InvalidOperationException($"Graph table {FormatPublicTableName(catalogTableName: storagePublicTableContext.Storage!.Definition!.QualifiedName)} must be mutated through its graph storage handle.");
        }
    }
}