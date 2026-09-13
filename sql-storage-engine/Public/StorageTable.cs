using sql_storage_engine.Buffers;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Overflow;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;
using sql_storage_engine.Transactions;

namespace sql_storage_engine;

internal sealed class StorageTable(TableStorage storage, StorageEngine owner, bool coordinate,
    bool flushMutations, Func<bool> isActive) : IStorageTable
{
    public CatalogTable Definition => storage.Definition;
    public async ValueTask<RowId> InsertAsync(Row row, CancellationToken cancellationToken = default)
    {
        EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureActive();
        RejectHistoryMutation();
        RejectGraphMutation();
        var id = await storage.InsertAsync(row, cancellationToken).ConfigureAwait(false);
        if (flushMutations) await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return id;
    }
    public async ValueTask<TableInsertResult> TryInsertAsync(Row row, CancellationToken cancellationToken = default)
    {
        EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureActive();
        RejectHistoryMutation();
        RejectGraphMutation();
        var result = await storage.TryInsertAsync(row, cancellationToken).ConfigureAwait(false);
        if (flushMutations) await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return result;
    }
    public async ValueTask<StoredRow?> GetAsync(RowId rowId, CancellationToken cancellationToken = default)
    {
        EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureActive();
        var result = await storage.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false);
        return result.Found ? new StoredRow(rowId, result.Row!) : null;
    }
    public async ValueTask<StoredRow?> GetAsync(RowId rowId, StorageReadOptions readOptions,
        CancellationToken cancellationToken = default)
    {
        EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureActive();
        ArgumentNullException.ThrowIfNull(readOptions);
        var result = await storage.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false);
        return result.Found ? new StoredRow(rowId, ApplyMasking(result.Row!, readOptions)) : null;
    }
    public IAsyncEnumerable<StoredRow> ScanAsync(
        CancellationToken cancellationToken = default) =>
        owner.TrackStream(ScanAsyncCore(cancellationToken), !coordinate);

    private async IAsyncEnumerable<StoredRow> ScanAsyncCore(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureActive();
        await foreach (var stored in storage.ScanAsync(cancellationToken).ConfigureAwait(false)) yield return stored;
    }
    public IAsyncEnumerable<StoredRow> ScanAsync(StorageReadOptions readOptions,
        CancellationToken cancellationToken = default) =>
        owner.TrackStream(ScanAsyncCore(readOptions, cancellationToken), !coordinate);

    private async IAsyncEnumerable<StoredRow> ScanAsyncCore(StorageReadOptions readOptions,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureActive();
        ArgumentNullException.ThrowIfNull(readOptions);
        await foreach (var stored in storage.ScanAsync(cancellationToken).ConfigureAwait(false))
            yield return stored with { Row = ApplyMasking(stored.Row, readOptions) };
    }
    public IAsyncEnumerable<StoredRow> SampleAsync(StorageTableSample sample,
        CancellationToken cancellationToken = default) =>
        owner.TrackStream(SampleAsyncCore(sample, cancellationToken), !coordinate);

    private async IAsyncEnumerable<StoredRow> SampleAsyncCore(StorageTableSample sample,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureActive();
        ArgumentNullException.ThrowIfNull(sample);
        await foreach (var stored in storage.SampleAsync(sample, cancellationToken).ConfigureAwait(false))
            yield return stored;
    }
    public IAsyncEnumerable<StoredRow> SampleAsync(StorageTableSample sample,
        StorageReadOptions readOptions,
        CancellationToken cancellationToken = default) =>
        owner.TrackStream(SampleAsyncCore(sample, readOptions, cancellationToken), !coordinate);

    private async IAsyncEnumerable<StoredRow> SampleAsyncCore(StorageTableSample sample,
        StorageReadOptions readOptions,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureActive();
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(readOptions);
        await foreach (var stored in storage.SampleAsync(sample, cancellationToken).ConfigureAwait(false))
            yield return stored with { Row = ApplyMasking(stored.Row, readOptions) };
    }
    public IAsyncEnumerable<StorageTemporalRow> TemporalScanAsync(StorageTemporalQuery query,
        CancellationToken cancellationToken = default) =>
        owner.TrackStream(TemporalScanAsyncCore(query, cancellationToken), !coordinate);

    private async IAsyncEnumerable<StorageTemporalRow> TemporalScanAsyncCore(StorageTemporalQuery query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureActive();
        ArgumentNullException.ThrowIfNull(query);
        await foreach (var stored in TemporalRowsAsync(query, cancellationToken).ConfigureAwait(false))
            yield return stored;
    }
    public IAsyncEnumerable<StorageTemporalRow> TemporalScanAsync(StorageTemporalQuery query,
        StorageReadOptions readOptions,
        CancellationToken cancellationToken = default) =>
        owner.TrackStream(TemporalScanAsyncCore(query, readOptions, cancellationToken), !coordinate);

    private async IAsyncEnumerable<StorageTemporalRow> TemporalScanAsyncCore(StorageTemporalQuery query,
        StorageReadOptions readOptions,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureActive(); using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureActive();
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(readOptions);
        await foreach (var stored in TemporalRowsAsync(query, cancellationToken).ConfigureAwait(false))
            yield return stored with { Row = ApplyMasking(stored.Row, readOptions) };
    }
    public async ValueTask<TableUpdateResult> UpdateAsync(RowId rowId, RowUpdate update, CancellationToken cancellationToken = default)
    {
        EnsureActive();
        if (coordinate && Definition.SystemVersioning is not null)
        {
            TableUpdateResult? atomicResult = null;
            await owner.ExecuteStatementAsync(async (statement, token) =>
            {
                var table = await statement.OpenTableAsync(Definition.Id, token).ConfigureAwait(false);
                atomicResult = await table.UpdateAsync(rowId, update, token).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
            return atomicResult!;
        }
        using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureActive();
        RejectHistoryMutation();
        RejectGraphMutation();
        var result = Definition.SystemVersioning is null
            ? await storage.UpdateAsync(rowId, update, cancellationToken).ConfigureAwait(false)
            : await UpdateTemporalAsync(rowId, update, cancellationToken).ConfigureAwait(false);
        if (flushMutations) await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return result;
    }
    public async ValueTask<TableDeleteResult> DeleteAsync(RowId rowId, CancellationToken cancellationToken = default)
    {
        EnsureActive();
        if (coordinate && Definition.SystemVersioning is not null)
        {
            TableDeleteResult? atomicResult = null;
            await owner.ExecuteStatementAsync(async (statement, token) =>
            {
                var table = await statement.OpenTableAsync(Definition.Id, token).ConfigureAwait(false);
                atomicResult = await table.DeleteAsync(rowId, token).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
            return atomicResult!;
        }
        using var lease = await EnterAsync(cancellationToken).ConfigureAwait(false); using var accessScope = owner.ActivateGate(lease); EnsureActive();
        RejectHistoryMutation();
        RejectGraphMutation();
        var result = Definition.SystemVersioning is null
            ? await storage.DeleteAsync(rowId, cancellationToken).ConfigureAwait(false)
            : await DeleteTemporalAsync(rowId, cancellationToken).ConfigureAwait(false);
        if (flushMutations) await owner.FlushAndPublishAsync(cancellationToken).ConfigureAwait(false); return result;
    }

    private async IAsyncEnumerable<StorageTemporalRow> TemporalRowsAsync(StorageTemporalQuery query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var temporal = Definition.SystemVersioning ?? throw new InvalidOperationException(
            $"Table {Definition.QualifiedName} is not system-versioned.");
        var history = await owner.OpenTableStorageAsync(temporal.HistoryTableId, cancellationToken)
            .ConfigureAwait(false);
        await foreach (var row in history.ScanAsync(cancellationToken).ConfigureAwait(false))
            if (MatchesTemporal(row.Row, temporal, query))
                yield return new StorageTemporalRow(temporal.HistoryTableId, row.RowId, row.Row);
        await foreach (var row in storage.ScanAsync(cancellationToken).ConfigureAwait(false))
            if (MatchesTemporal(row.Row, temporal, query))
                yield return new StorageTemporalRow(Definition.Id, row.RowId, row.Row);
    }

    private async ValueTask<TableUpdateResult> UpdateTemporalAsync(RowId rowId, RowUpdate update,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        var old = await storage.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false);
        if (!old.Found) return new TableUpdateResult(false, rowId, rowId);
        var (history, archived, timestamp) = await ArchiveAsync(old.Row!, cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await storage.UpdateSystemVersionedAsync(rowId, update, timestamp, cancellationToken)
                .ConfigureAwait(false);
            if (!result.Updated) await history.DeleteAsync(archived, CancellationToken.None).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await history.DeleteAsync(archived, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<TableDeleteResult> DeleteTemporalAsync(RowId rowId,
        CancellationToken cancellationToken)
    {
        var old = await storage.TryGetAsync(rowId, cancellationToken).ConfigureAwait(false);
        if (!old.Found) return new TableDeleteResult(false, []);
        var (history, archived, _) = await ArchiveAsync(old.Row!, cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await storage.DeleteAsync(rowId, cancellationToken).ConfigureAwait(false);
            if (!result.Deleted) await history.DeleteAsync(archived, CancellationToken.None).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await history.DeleteAsync(archived, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<(TableStorage History, RowId Archived, SqlValue Timestamp)> ArchiveAsync(Row oldRow,
        CancellationToken cancellationToken)
    {
        var temporal = Definition.SystemVersioning!;
        var history = await owner.OpenTableStorageAsync(temporal.HistoryTableId, cancellationToken)
            .ConfigureAwait(false);
        var endPosition = ColumnPosition(temporal.PeriodEndColumnId);
        var startColumn = Definition.Columns[ColumnPosition(temporal.PeriodStartColumnId)];
        var timestamp = SqlConversion.ConvertTo(startColumn.Type,
            await owner.AllocateGeneratedValueAsync(CatalogGeneratedAlwaysKind.RowStart, cancellationToken)
                .ConfigureAwait(false));
        var values = oldRow.Values.ToArray();
        if (((DateTimeSqlValue)timestamp).Value <
            ((DateTimeSqlValue)values[ColumnPosition(temporal.PeriodStartColumnId)]).Value)
            throw new InvalidOperationException("The temporal clock moved before the current row's period start.");
        values[endPosition] = timestamp;
        var inserted = await history.InsertPreparedAsync(new Row(values), cancellationToken).ConfigureAwait(false);
        if (!inserted.Inserted) throw new InvalidOperationException("A temporal history insert was unexpectedly ignored.");
        return (history, inserted.RowId!.Value, timestamp);
    }

    private bool MatchesTemporal(Row row, CatalogSystemVersioning temporal, StorageTemporalQuery query)
    {
        var start = ((DateTimeSqlValue)row.Values[ColumnPosition(temporal.PeriodStartColumnId)]).Value;
        var end = ((DateTimeSqlValue)row.Values[ColumnPosition(temporal.PeriodEndColumnId)]).Value;
        return query switch
        {
            StorageTemporalAsOf asOf => start <= asOf.Instant && end > asOf.Instant,
            StorageTemporalFromTo range => start < range.End && end > range.Start,
            StorageTemporalBetweenAnd range => start <= range.End && end > range.Start,
            StorageTemporalContainedIn range => start >= range.Start && end <= range.End,
            StorageTemporalAll => true,
            _ => throw new ArgumentOutOfRangeException(nameof(query), query, "Unknown temporal query type.")
        };
    }

    private int ColumnPosition(ColumnId id) => Definition.Columns.Select((column, position) => (column, position))
        .Single(item => item.column.Id == id).position;

    private void RejectHistoryMutation()
    {
        if (owner.TryGetTemporalCurrent(Definition.Id, out var current))
            throw new InvalidOperationException(
                $"Temporal history table {Definition.QualifiedName} is maintained by {current!.QualifiedName}.");
    }

    private void RejectGraphMutation()
    {
        if (Definition.Graph is not null)
            throw new InvalidOperationException(
                $"Graph table {Definition.QualifiedName} must be mutated through its graph storage handle.");
    }

    private void EnsureActive()
    {
        if (!isActive()) throw new InvalidOperationException("The statement scope has completed.");
    }
    private async ValueTask<IDisposable> EnterAsync(CancellationToken cancellationToken)
        => await owner.EnterStatementGateAsync(cancellationToken, reentrant: !coordinate).ConfigureAwait(false);

    private Row ApplyMasking(Row row, StorageReadOptions readOptions) => new(row.Values.Select((value, position) =>
    {
        var column = Definition.Columns[position];
        return readOptions.CanUnmask(column.Id) ? value : SqlDataMasking.Apply(column, value);
    }));
}
