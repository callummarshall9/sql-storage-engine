// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;
using sql_storage_engine.Tables;
using sql_storage_engine.Heap;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Services.Managements.HeapIndexMutations;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private readonly Dictionary<ulong, HeapIndexMutationManagementService> heapIndexMutations = new();
    private async IAsyncEnumerable<StoredRow> ScanTableRowsAsync(TableStorageContext tableStorageContext, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var entry in ScanHeapBytes(tableStorageContext: tableStorageContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return new StoredRow
            {
                RowId = entry.RowId,
                Row = ProjectColumnSet(tableStorageContext: tableStorageContext, row: await DecodeTableRowAsync(tableStorageContext: tableStorageContext, source: entry.Row, tableDefinition: tableStorageContext.Schema!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
            };
        }
    }

    private async IAsyncEnumerable<StoredRow> SampleTableRowsAsync(TableStorageContext tableStorageContext, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var percentage = tableStorageContext.RequestedRows is { } requestedRows ? await RowSamplePercentageAsync(tableStorageContext: tableStorageContext, requestedRows: requestedRows, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) : tableStorageContext.PagePercentage;

        await foreach (var entry in SampleHeapBytes(tableStorageContext: tableStorageContext, pagePercentage: percentage, repeatableSeed: tableStorageContext.RepeatableSeed, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return new StoredRow
            {
                RowId = entry.RowId,
                Row = ProjectColumnSet(tableStorageContext: tableStorageContext, row: await DecodeTableRowAsync(tableStorageContext: tableStorageContext, source: entry.Row, tableDefinition: tableStorageContext.Schema!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
            };
        }
    }

    private async ValueTask<decimal> RowSamplePercentageAsync(TableStorageContext tableStorageContext, long requestedRows, CancellationToken cancellationToken)
    {
        if (requestedRows == 0)
        {
            return 0;
        }

        var liveRows = await CountHeapRowsAsync(tableStorageContext: tableStorageContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (liveRows == 0 || requestedRows >= liveRows)
        {
            return 100;
        }

        return (decimal)requestedRows * 100m / liveRows;
    }

    private async ValueTask<TableInsertResult> TryPersistTableRowAsync(TableStorageContext tableStorageContext, Row row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(argument: row);

        row = await PrepareTableInsertRowAsync(tableStorageContext: tableStorageContext, row: row, graphMutation: false, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return await PersistPreparedTableRowAsync(tableStorageContext: tableStorageContext, row: row, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async ValueTask<TableInsertResult> PersistTableGraphRowAsync(TableStorageContext tableStorageContext, Row row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(argument: row);

        row = await PrepareTableInsertRowAsync(tableStorageContext: tableStorageContext, row: row, graphMutation: true, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return await PersistPreparedTableRowAsync(tableStorageContext: tableStorageContext, row: row, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async ValueTask<TableInsertResult> PersistPreparedTableRowAsync(TableStorageContext tableStorageContext, Row row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(argument: row);
        AdmitTableRow(tableDefinition: tableStorageContext.Schema!, row: row);

        RowEncodingResult encoded = await EncodeTableRowAsync(tableStorageContext: tableStorageContext, row: row, tableDefinition: tableStorageContext.Schema!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        RowId? rowId = null;
        List<(TableIndexBinding Index, sql_storage_engine.Models.Indexes.IndexKey Key)> insertedIndexes = [];
        HashSet<OverflowReference> reclaimed = [];
        var indexes = CurrentIndexes(tableStorageContext: tableStorageContext);

        try
        {
            var storedRow = await DecodeTableRowAsync(tableStorageContext: tableStorageContext, source: encoded.Bytes, tableDefinition: tableStorageContext.Schema!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            foreach (var index in indexes.Where(predicate: candidate => candidate.Definition!.IsUnique && candidate.Definition!.IgnoreDuplicateKey))
            {
                sql_storage_engine.Models.Indexes.IndexKey key = EncodeTableIndexKey(row: storedRow, catalogTable: tableStorageContext.Definition!, catalogIndex: index.Definition!);

                if ((await FindIndexReferencesAsync(tableStorageContext: tableStorageContext, tableIndexBinding: index, indexKey: key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Count == 0)
                {
                    continue;
                }

                foreach (var reference in encoded.NewlyAllocated.Reverse())
                {
                    await FreeTableOverflowAsync(tableStorageContext: tableStorageContext, overflowReference: reference, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                    reclaimed.Add(item: reference);
                }

                return new TableInsertResult
                {
                    Inserted = false,
                    RowId = null,
                    Warnings = [$"Duplicate key was ignored by index '{index.Definition!.Name}'."]
                };
            }

            rowId = await PersistHeapBytesAsync(tableStorageContext: tableStorageContext, row: encoded.Bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            foreach (var index in indexes)
            {
                foreach (sql_storage_engine.Models.Indexes.IndexKey key in EncodeIndexKeys(row: storedRow, catalogTable: tableStorageContext.Definition!, catalogIndex: index.Definition!))
                {
                    await PersistIndexReferenceAsync(tableStorageContext: tableStorageContext, tableIndexBinding: index, newIndexKey: key, newRowId: rowId.Value, includedPayload: EncodeIndexIncludedValues(row: storedRow, catalogTable: tableStorageContext.Definition!, catalogIndex: index.Definition!), token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                    insertedIndexes.Add(item: (index, key));
                }
            }

            await PublishCatalogIndexRootsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return new TableInsertResult
            {
                Inserted = true,
                RowId = rowId.Value,
                Warnings = []
            };
        }
        catch (Exception exception)
        {
            List<PageId> unreclaimed = [];

            for (var index = insertedIndexes.Count - 1; index >= 0; index--)
            {
                try
                {
                    await RemoveIndexReferenceAsync(tableStorageContext: tableStorageContext, tableIndexBinding: insertedIndexes[index].Index, indexKey: insertedIndexes[index].Key, rowId: rowId!.Value)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (StorageException)
                {
                    unreclaimed.Add(item: insertedIndexes[index].Index.Definition!.RootPageId);
                }
            }

            if (rowId is { } insertedRow)
            {
                try
                {
                    if (!await RemoveHeapBytesAsync(tableStorageContext: tableStorageContext, deletedRowId: insertedRow)
            .ConfigureAwait(continueOnCapturedContext: false))
                    {
                        unreclaimed.Add(item: insertedRow.PageId);
                    }
                }
                catch (StorageException)
                {
                    unreclaimed.Add(item: insertedRow.PageId);
                }
            }

            foreach (var reference in encoded.NewlyAllocated.Reverse())
            {
                if (reclaimed.Contains(item: reference))
                {
                    continue;
                }

                try
                {
                    await FreeTableOverflowAsync(tableStorageContext: tableStorageContext, overflowReference: reference)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (StorageException)
                {
                    unreclaimed.Add(item: reference.FirstPageId);
                }
            }

            try
            {
                await PublishCatalogIndexRootsAsync(cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (StorageException)
            {
                unreclaimed.AddRange(collection: indexes.Select(selector: index => index.Definition!.RootPageId));
            }

            throw new TableMutationException(message: "Table insertion failed and compensating cleanup was attempted.", unreclaimedPageIds: unreclaimed.Distinct()
            .ToArray(), innerException: exception);
        }
    }

    private async ValueTask<(bool Found, Row? Row)> ReadLogicalTableRowAsync(TableStorageContext tableStorageContext, RowId rowId, CancellationToken cancellationToken = default)
    {
        var lockContext = await heapIndexMutations[tableStorageContext.MutationId].PrepareHeapIndexMutationContextReadAsync(heapIndexMutationContext: new HeapIndexMutationContext { RowId = rowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var result = await ReadHeapBytesAsync(tableStorageContext: tableStorageContext, rowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (result.Result != TableHeapLookupResult.Found)
            {
                return (false, null);
            }

            return (true, ProjectColumnSet(tableStorageContext: tableStorageContext, row: await DecodeTableRowAsync(tableStorageContext: tableStorageContext, source: result.Row, tableDefinition: tableStorageContext.Schema!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)));
        }
        finally
        {
            heapIndexMutations[tableStorageContext.MutationId].FinishHeapIndexMutationContextRead(heapIndexMutationContext: lockContext);
        }
    }

    private ValueTask<TableUpdateResult> ApplyTableRowUpdateAsync(TableStorageContext tableStorageContext, RowId rowId, RowUpdate update, CancellationToken cancellationToken = default) =>
        ApplyTableRowUpdateCoreAsync(tableStorageContext: tableStorageContext, rowId: rowId, update: update, periodStart: null, graphMutation: false, cancellationToken: cancellationToken);

    private ValueTask<TableUpdateResult> ApplyTableGraphRowUpdateAsync(TableStorageContext tableStorageContext, RowId rowId, RowUpdate update, CancellationToken cancellationToken = default) =>
        ApplyTableRowUpdateCoreAsync(tableStorageContext: tableStorageContext, rowId: rowId, update: update, periodStart: null, graphMutation: true, cancellationToken: cancellationToken);

    private ValueTask<TableUpdateResult> ApplyTableVersionedRowUpdateAsync(TableStorageContext tableStorageContext, RowId rowId, RowUpdate update, SqlValue periodStart, CancellationToken cancellationToken = default) =>
        ApplyTableRowUpdateCoreAsync(tableStorageContext: tableStorageContext, rowId: rowId, update: update, periodStart: periodStart, graphMutation: false, cancellationToken: cancellationToken);

    private async ValueTask<TableUpdateResult> ApplyTableRowUpdateCoreAsync(TableStorageContext tableStorageContext, RowId rowId, RowUpdate update, SqlValue? periodStart, bool graphMutation, CancellationToken cancellationToken)
    {
        await heapIndexMutations[tableStorageContext.MutationId].PrepareHeapIndexMutationContextWriteAsync(heapIndexMutationContext: new HeapIndexMutationContext { RowId = rowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        ArgumentNullException.ThrowIfNull(argument: update);

        var current = await ReadHeapBytesAsync(tableStorageContext: tableStorageContext, rowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (current.Result != TableHeapLookupResult.Found)
        {
            return new TableUpdateResult
            {
                Updated = false,
                PreviousRowId = rowId,
                CurrentRowId = rowId,
                Relocated = false
            };
        }

        var oldBytes = current.Row.ToArray();

        var oldRow = await DecodeTableRowAsync(tableStorageContext: tableStorageContext, source: current.Row, tableDefinition: tableStorageContext.Schema!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        update = await PrepareTableUpdateRowAsync(tableStorageContext: tableStorageContext, oldRow: oldRow, update: update, periodStart: periodStart, graphMutation: graphMutation, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var replacement = await EncodeTableRowUpdateAsync(tableStorageContext: tableStorageContext, currentRow: current.Row, rowUpdate: update, tableDefinition: tableStorageContext.Schema!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var newRow = await DecodeTableRowAsync(tableStorageContext: tableStorageContext, source: replacement.Bytes, tableDefinition: tableStorageContext.Schema!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var heapResult = await ReplaceHeapBytesAsync(tableStorageContext: tableStorageContext, updatedRowId: rowId, row: replacement.Bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        RowId newRowId = rowId;
        var relocated = heapResult == HeapUpdateResult.RelocationRequired;

        if (relocated)
        {
            newRowId = await PersistHeapBytesAsync(tableStorageContext: tableStorageContext, row: replacement.Bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await heapIndexMutations[tableStorageContext.MutationId].PrepareHeapIndexMutationContextWriteAsync(heapIndexMutationContext: new HeapIndexMutationContext { RowId = newRowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        else if (heapResult != HeapUpdateResult.Updated)
        {
            foreach (var reference in replacement.NewlyAllocated)
            {
                await FreeTableOverflowAsync(tableStorageContext: tableStorageContext, overflowReference: reference)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            return new TableUpdateResult
            {
                Updated = false,
                PreviousRowId = rowId,
                CurrentRowId = rowId,
                Relocated = false
            };
        }

        List<(TableIndexBinding Index, sql_storage_engine.Models.Indexes.IndexKey Key, byte[] Payload)> removedEntries = [];
        List<(TableIndexBinding Index, sql_storage_engine.Models.Indexes.IndexKey Key, byte[] Payload)> addedEntries = [];
        var indexes = CurrentIndexes(tableStorageContext: tableStorageContext);

        try
        {
            foreach (var index in indexes)
            {
                var oldKeys = EncodeIndexKeys(row: oldRow, catalogTable: tableStorageContext.Definition!, catalogIndex: index.Definition!)
            .ToHashSet();

                var newKeys = EncodeIndexKeys(row: newRow, catalogTable: tableStorageContext.Definition!, catalogIndex: index.Definition!)
            .ToHashSet();

                var oldPayload = EncodeIndexIncludedValues(row: oldRow, catalogTable: tableStorageContext.Definition!, catalogIndex: index.Definition!);
                var newPayload = EncodeIndexIncludedValues(row: newRow, catalogTable: tableStorageContext.Definition!, catalogIndex: index.Definition!);

                var payloadChanged = !oldPayload.AsSpan()
            .SequenceEqual(other: newPayload);

                var remove = relocated || payloadChanged ? oldKeys : oldKeys.Except(second: newKeys);
                var add = relocated || payloadChanged ? newKeys : newKeys.Except(second: oldKeys);

                foreach (sql_storage_engine.Models.Indexes.IndexKey key in remove)
                {
                    if (!await RemoveIndexReferenceAsync(tableStorageContext: tableStorageContext, tableIndexBinding: index, indexKey: key, rowId: rowId, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
                    {
                        throw new StorageCorruptionException(message: $"Index {index.Definition!.Id} is missing the row being updated.");
                    }

                    removedEntries.Add(item: (index, key, oldPayload));
                }

                foreach (sql_storage_engine.Models.Indexes.IndexKey key in add)
                {
                    await PersistIndexReferenceAsync(tableStorageContext: tableStorageContext, tableIndexBinding: index, newIndexKey: key, newRowId: newRowId, includedPayload: newPayload, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                    addedEntries.Add(item: (index, key, newPayload));
                }
            }

            if (relocated && !await RemoveHeapBytesAsync(tableStorageContext: tableStorageContext, deletedRowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
            {
                throw new StorageCorruptionException(message: "Relocated row's previous heap slot could not be deleted.");
            }

            foreach (var reference in replacement.Retired)
            {
                await FreeTableOverflowAsync(tableStorageContext: tableStorageContext, overflowReference: reference, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            await PublishCatalogIndexRootsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return new TableUpdateResult
            {
                Updated = true,
                PreviousRowId = rowId,
                CurrentRowId = newRowId,
                Relocated = rowId != newRowId
            };
        }
        catch (Exception exception)
        {
            List<PageId> unreclaimed = [];

            for (var index = addedEntries.Count - 1; index >= 0; index--)
            {
                try
                {
                    await RemoveIndexReferenceAsync(tableStorageContext: tableStorageContext, tableIndexBinding: addedEntries[index].Index, indexKey: addedEntries[index].Key, rowId: newRowId)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (StorageException)
                {
                    unreclaimed.Add(item: addedEntries[index].Index.Definition!.RootPageId);
                }
            }

            for (var index = removedEntries.Count - 1; index >= 0; index--)
            {
                try
                {
                    await PersistIndexReferenceAsync(tableStorageContext: tableStorageContext, tableIndexBinding: removedEntries[index].Index, newIndexKey: removedEntries[index].Key, newRowId: rowId, includedPayload: removedEntries[index].Payload, token: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (StorageException)
                {
                    unreclaimed.Add(item: removedEntries[index].Index.Definition!.RootPageId);
                }
            }

            try
            {
                if (relocated)
                {
                    await RemoveHeapBytesAsync(tableStorageContext: tableStorageContext, deletedRowId: newRowId)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                else if (await ReplaceHeapBytesAsync(tableStorageContext: tableStorageContext, updatedRowId: rowId, row: oldBytes)
            .ConfigureAwait(continueOnCapturedContext: false) != HeapUpdateResult.Updated)
                {
                    unreclaimed.Add(item: rowId.PageId);
                }
            }
            catch (StorageException)
            {
                unreclaimed.Add(item: newRowId.PageId);
            }

            foreach (var reference in replacement.NewlyAllocated)
            {
                try
                {
                    await FreeTableOverflowAsync(tableStorageContext: tableStorageContext, overflowReference: reference)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (StorageException)
                {
                    unreclaimed.Add(item: reference.FirstPageId);
                }
            }

            try
            {
                await PublishCatalogIndexRootsAsync(cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (StorageException)
            {
                unreclaimed.AddRange(collection: indexes.Select(selector: index => index.Definition!.RootPageId));
            }

            throw new TableMutationException(message: "Table update failed and the previous logical state was restored.", unreclaimedPageIds: unreclaimed.Distinct()
            .ToArray(), innerException: exception);
        }
    }

    private async ValueTask<Row> PrepareTableInsertRowAsync(TableStorageContext tableStorageContext, Row row, bool graphMutation, CancellationToken cancellationToken)
    {
        if (row.Values.Count != tableStorageContext.Schema!.Columns.Count)
        {
            throw new ArgumentException(message: "Row width does not match schema width.", paramName: nameof(row));
        }

        var values = row.Values.ToArray();
        SqlValue? suppliedColumnSet = null;
        var context = new Dictionary<string, SqlValue>(comparer: StringComparer.OrdinalIgnoreCase);

        for (var position = 0; position < tableStorageContext.Definition!.Columns.Count; position++)
        {
            var column = tableStorageContext.Definition!.Columns[position];
            var supplied = values[position];

            if (column.Identity is not null)
            {
                if (supplied is not DefaultSqlValue && !supplied.IsNull)
                {
                    throw new ArgumentException(message: $"IDENTITY column '{column.Name}' cannot be assigned explicitly.", paramName: nameof(row));
                }

                values[position] = await AllocateIdentityAsync(tableId: tableStorageContext.Definition!.Id, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            else if (column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp)
            {
                values[position] = await AllocateTableRowVersionAsync(tableStorageContext: tableStorageContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            else if (column.GeneratedAlways != CatalogGeneratedAlwaysKind.None)
            {
                var isGraphColumn = column.GeneratedAlways is CatalogGeneratedAlwaysKind.GraphIdentity or CatalogGeneratedAlwaysKind.GraphFromNode or CatalogGeneratedAlwaysKind.GraphToNode;

                if (graphMutation && isGraphColumn)
                {
                    if (supplied is DefaultSqlValue || supplied.IsNull)
                    {
                        throw new ArgumentException(message: $"Graph-generated column '{column.Name}' requires an engine value.", paramName: nameof(row));
                    }

                    values[position] = ConvertTableValue(sqlType: column.Type, sqlValue: supplied);
                }
                else
                {
                    if (supplied is not DefaultSqlValue && !supplied.IsNull)
                    {
                        throw new ArgumentException(message: $"Generated-always column '{column.Name}' cannot be assigned explicitly.", paramName: nameof(row));
                    }

                    values[position] = ConvertTableValue(sqlType: column.Type, sqlValue: await AllocateTableGeneratedValueAsync(tableStorageContext: tableStorageContext, kind: column.GeneratedAlways, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));
                }
            }
            else if (column.IsComputed)
            {
                if (supplied is not DefaultSqlValue && !supplied.IsNull)
                {
                    throw new ArgumentException(message: $"Computed column '{column.Name}' cannot be assigned explicitly.", paramName: nameof(row));
                }

                values[position] = new DefaultSqlValue();
            }
            else if (column.IsColumnSet)
            {
                suppliedColumnSet = supplied;

                values[position] = new NullSqlValue
                {
                    IsNull = true
                };
            }
            else
            {
                if (supplied is DefaultSqlValue)
                {
                    supplied = column.DefaultExpression is null ? new NullSqlValue
                    {
                        IsNull = true
                    }

                    : EvaluateTableExpression(expression: column.DefaultExpression, columns: context);
                }

                values[position] = supplied.IsNull ? supplied : ConvertTableValue(sqlType: column.Type, sqlValue: supplied);
            }

            if (values[position] is not DefaultSqlValue)
            {
                context[column.Name] = values[position];
            }
        }

        if (suppliedColumnSet is not null and not DefaultSqlValue && !suppliedColumnSet.IsNull)
        {
            if (tableStorageContext.Definition!.Columns.Select(selector: (column, index) => (column, index))
            .Any(predicate: item => item.column.IsSparse && !row.Values[item.index].IsNull && row.Values[item.index] is not DefaultSqlValue))
            {
                throw new ArgumentException(message: "Assign either individual SPARSE columns or the XML column set, not both.", paramName: nameof(row));
            }

            ApplyColumnSet(tableStorageContext: tableStorageContext, values: values, supplied: suppliedColumnSet, context: context, clearExisting: true);
        }

        ComputeColumns(tableStorageContext: tableStorageContext, values: values, context: context);
        ClearColumnSet(tableStorageContext: tableStorageContext, values: values, context: context);

        var result = new Row
        {
            Values = Array.AsReadOnly(array: values)
        };

        AdmitTableRow(tableDefinition: tableStorageContext.Schema!, row: result);
        ValidateChecks(tableStorageContext: tableStorageContext, row: result);
        return result;
    }

    private async ValueTask<RowUpdate> PrepareTableUpdateRowAsync(TableStorageContext tableStorageContext, Row oldRow, RowUpdate update, SqlValue? periodStart, bool graphMutation, CancellationToken cancellationToken)
    {
        if (update.Columns.Select(selector: column => column.ColumnIndex)
            .Distinct()
            .Count() != update.Columns.Count)
        {
            throw new ArgumentException(message: "Updated column indexes must be unique.", paramName: nameof(update));
        }

        var values = oldRow.Values.ToArray();

        var context = tableStorageContext.Definition!.Columns.Select(selector: (column, index) => (column.Name, Value: values[index]))
            .ToDictionary(keySelector: item => item.Name, elementSelector: item => item.Value, comparer: StringComparer.OrdinalIgnoreCase);

        ColumnUpdate? columnSetUpdate = update.Columns.Where(predicate: changed => changed.ColumnIndex >= 0 && changed.ColumnIndex < tableStorageContext.Definition!.Columns.Count && tableStorageContext.Definition!.Columns[changed.ColumnIndex].IsColumnSet)
            .Select(selector: changed => (ColumnUpdate? )changed)
            .SingleOrDefault();

        if (columnSetUpdate is not null && update.Columns.Any(predicate: changed => changed.ColumnIndex >= 0 && changed.ColumnIndex < tableStorageContext.Definition!.Columns.Count && tableStorageContext.Definition!.Columns[changed.ColumnIndex].IsSparse))
        {
            throw new ArgumentException(message: "Update either individual SPARSE columns or the XML column set, not both.", paramName: nameof(update));
        }

        foreach (var changed in update.Columns)
        {
            if (changed.ColumnIndex < 0 || changed.ColumnIndex >= tableStorageContext.Definition!.Columns.Count)
            {
                throw new ArgumentOutOfRangeException(paramName: nameof(update), message: "Updated column index is outside the schema.");
            }

            var column = tableStorageContext.Definition!.Columns[changed.ColumnIndex];

            if (column.Identity is not null)
            {
                throw new ArgumentException(message: "An IDENTITY column cannot be updated.", paramName: nameof(update));
            }

            if (column.IsComputed)
            {
                throw new ArgumentException(message: "A computed column cannot be updated directly.", paramName: nameof(update));
            }

            if (column.IsColumnSet)
            {
                continue;
            }

            if (column.GeneratedAlways != CatalogGeneratedAlwaysKind.None && !(graphMutation && (column.GeneratedAlways is CatalogGeneratedAlwaysKind.GraphIdentity or CatalogGeneratedAlwaysKind.GraphFromNode or CatalogGeneratedAlwaysKind.GraphToNode)))
            {
                throw new ArgumentException(message: "A generated-always column cannot be updated directly.", paramName: nameof(update));
            }

            if (column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp)
            {
                throw new ArgumentException(message: "A rowversion column cannot be assigned explicitly.", paramName: nameof(update));
            }

            var value = changed.Value;

            if (value is DefaultSqlValue)
            {
                value = column.DefaultExpression is null ? new NullSqlValue
                {
                    IsNull = true
                }

                : EvaluateTableExpression(expression: column.DefaultExpression, columns: context);
            }

            values[changed.ColumnIndex] = value.IsNull ? value : ConvertTableValue(sqlType: column.Type, sqlValue: value);
            context[column.Name] = values[changed.ColumnIndex];
        }

        if (columnSetUpdate is not null)
        {
            ApplyColumnSet(tableStorageContext: tableStorageContext, values: values, supplied: columnSetUpdate.Value.Value, context: context, clearExisting: true);
        }

        var rowVersionPosition = GetRowVersionPosition(tableStorageContext: tableStorageContext);

        if (rowVersionPosition >= 0)
        {
            values[rowVersionPosition] = await AllocateTableRowVersionAsync(tableStorageContext: tableStorageContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            context[tableStorageContext.Definition!.Columns[rowVersionPosition].Name] = values[rowVersionPosition];
        }

        for (var position = 0; position < tableStorageContext.Definition!.Columns.Count; position++)
        {
            var column = tableStorageContext.Definition!.Columns[position];

            if (column.GeneratedAlways is CatalogGeneratedAlwaysKind.None or CatalogGeneratedAlwaysKind.RowEnd)
            {
                continue;
            }

            if (column.GeneratedAlways is CatalogGeneratedAlwaysKind.GraphIdentity or CatalogGeneratedAlwaysKind.GraphFromNode or CatalogGeneratedAlwaysKind.GraphToNode)
            {
                continue;
            }

            values[position] = column.GeneratedAlways == CatalogGeneratedAlwaysKind.RowStart && periodStart is not null ? ConvertTableValue(sqlType: column.Type, sqlValue: periodStart) : ConvertTableValue(sqlType: column.Type, sqlValue: await AllocateTableGeneratedValueAsync(tableStorageContext: tableStorageContext, kind: column.GeneratedAlways, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

            context[column.Name] = values[position];
        }

        ComputeColumns(tableStorageContext: tableStorageContext, values: values, context: context);
        ClearColumnSet(tableStorageContext: tableStorageContext, values: values, context: context);

        var result = new Row
        {
            Values = Array.AsReadOnly(array: values)
        };

        AdmitTableRow(tableDefinition: tableStorageContext.Schema!, row: result);
        ValidateChecks(tableStorageContext: tableStorageContext, row: result);

        return new RowUpdate
        {
            Columns = Array.AsReadOnly(array: values.Select(selector: (value, index) => (value, index))
            .Where(predicate: item => !Equals(objA: item.value, objB: oldRow.Values[item.index]))
            .Select(selector: item => new ColumnUpdate { ColumnIndex = item.index, Value = item.value })
            .ToArray())
        };
    }

    private void ComputeColumns(TableStorageContext tableStorageContext, SqlValue[] values, IDictionary<string, SqlValue> context)
    {
        foreach (var position in ReadComputedColumnOrder(columns: tableStorageContext.Definition!.Columns))
        {
            var column = tableStorageContext.Definition!.Columns[position];
            var computed = EvaluateTableExpression(expression: column.ComputedExpression!, columns: (IReadOnlyDictionary<string, SqlValue>)context);
            var source = ResolveComputedColumnReference(expression: column.ComputedExpression!, columns: tableStorageContext.Definition!.Columns);
            values[position] = computed.IsNull ? computed : source is null ? ConvertTableValue(sqlType: column.Type, sqlValue: computed) : ConvertTableValue(sqlTypes: [column.Type, source.Type], sqlValue: computed);
            context[column.Name] = values[position];
        }
    }

    private Row ProjectColumnSet(TableStorageContext tableStorageContext, Row row)
    {
        var position = tableStorageContext.Definition!.Columns.Select(selector: (column, index) => (column, index))
            .Where(predicate: item => item.column.IsColumnSet)
            .Select(selector: item => item.index)
            .DefaultIfEmpty(defaultValue: -1)
            .Single();

        if (position < 0)
        {
            return row;
        }

        var values = row.Values.ToArray();

        var elements = tableStorageContext.Definition!.Columns.Select(selector: (column, index) => (column, index))
            .Where(predicate: item => item.column.IsSparse && !values[item.index].IsNull)
            .Select(selector: item => new ColumnSetXmlElement { Name = item.column.Name, Value = ((TextSqlValue)ConvertTableValue(sqlType: ReadProjectionType(sqlTypeName: SqlTypeName.NVarChar), sqlValue: values[item.index])).Value });

        values[position] = new TextSqlValue
        {
            Value = rowCodec.WriteRowEncodingContextColumnSet(rowEncodingContext: new RowEncodingContext { ColumnSetElements = elements.ToArray() })
            .Text!,
            StorageFamily = SqlStorageFamily.Text
        };

        return new Row
        {
            Values = Array.AsReadOnly(array: values)
        };
    }

    private void ApplyColumnSet(TableStorageContext tableStorageContext, SqlValue[] values, SqlValue supplied, IDictionary<string, SqlValue> context, bool clearExisting)
    {
        if (clearExisting)
        {
            foreach (var item in tableStorageContext.Definition!.Columns.Select(selector: (column, index) => (column, index))
            .Where(predicate: item => item.column.IsSparse))
            {
                values[item.index] = new NullSqlValue
                {
                    IsNull = true
                };

                context[item.column.Name] = new NullSqlValue
                {
                    IsNull = true
                };
            }
        }

        if (supplied.IsNull)
        {
            return;
        }

        var xml = ((TextSqlValue)ConvertTableValue(sqlType: ReadProjectionType(sqlTypeName: SqlTypeName.Xml), sqlValue: supplied)).Value;

        var elements = rowCodec.ReadRowEncodingContextColumnSet(rowEncodingContext: new RowEncodingContext { Text = xml })
            .ColumnSetElements!;

        var sparseByName = tableStorageContext.Definition!.Columns.Select(selector: (column, index) => (column, index))
            .Where(predicate: item => item.column.IsSparse)
            .ToDictionary(keySelector: item => item.column.Name, elementSelector: item => item, comparer: StringComparer.Ordinal);

        HashSet<string> assigned = new(comparer: StringComparer.Ordinal);

        foreach (var element in elements)
        {
            if (element.NamespaceName!.Length != 0 || !sparseByName.TryGetValue(key: element.Name!, value: out var item))
            {
                throw new ArgumentException(message: $"Column-set element '{element.Name}' does not identify a SPARSE column.", paramName: nameof(supplied));
            }

            if (!assigned.Add(item: item.column.Name))
            {
                throw new ArgumentException(message: $"SPARSE column '{item.column.Name}' occurs more than once in the column set.", paramName: nameof(supplied));
            }

            var nil = element.Nil;

            SqlValue value = string.Equals(a: nil, b: "true", comparisonType: StringComparison.OrdinalIgnoreCase) || nil == "1" ? new NullSqlValue
            {
                IsNull = true
            }

            : ConvertTableValue(sqlType: item.column.Type, sqlValue: new TextSqlValue { Value = NormalizeColumnSetText(tableStorageContext: tableStorageContext, type: item.column.Type, value: element.Value!), StorageFamily = SqlStorageFamily.Text });

            values[item.index] = value;
            context[item.column.Name] = value;
        }
    }

    private void ClearColumnSet(TableStorageContext tableStorageContext, SqlValue[] values, IDictionary<string, SqlValue> context)
    {
        var position = tableStorageContext.Definition!.Columns.Select(selector: (column, index) => (column, index))
            .Where(predicate: item => item.column.IsColumnSet)
            .Select(selector: item => item.index)
            .DefaultIfEmpty(defaultValue: -1)
            .Single();

        if (position < 0)
        {
            return;
        }

        values[position] = new NullSqlValue
        {
            IsNull = true
        };

        context[tableStorageContext.Definition!.Columns[position].Name] = new NullSqlValue
        {
            IsNull = true
        };
    }

    private static string NormalizeColumnSetText(TableStorageContext tableStorageContext, SqlType type, string value)
    {
        if (value.Length != 0)
        {
            return value;
        }

        return type.Name is SqlTypeName.Bit or SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or SqlTypeName.BigInt or SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.Real or SqlTypeName.Float or SqlTypeName.Money or SqlTypeName.SmallMoney ? "0" : value;
    }

    private void ValidateChecks(TableStorageContext tableStorageContext, Row row)
    {
        if (tableStorageContext.Definition!.CheckConstraints.Count == 0)
        {
            return;
        }

        var context = tableStorageContext.Definition!.Columns.Select(selector: (column, index) => (column.Name, Value: row.Values[index]))
            .ToDictionary(keySelector: item => item.Name, elementSelector: item => item.Value, comparer: StringComparer.OrdinalIgnoreCase);

        foreach (var check in tableStorageContext.Definition!.CheckConstraints)
        {
            var result = EvaluateTableExpression(expression: check.Expression, columns: context);

            if (!result.IsNull && !((BooleanSqlValue)ConvertTableValue(sqlType: ReadProjectionType(sqlTypeName: SqlTypeName.Bit), sqlValue: result)).Value)
            {
                throw new ArgumentException(message: $"CHECK constraint '{check.Name}' rejected the row.", paramName: nameof(row));
            }
        }
    }

    private int GetRowVersionPosition(TableStorageContext tableStorageContext) =>
        tableStorageContext.Definition!.Columns.Select(selector: (column, index) => (column, index))
            .Where(predicate: item => item.column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp)
            .Select(selector: item => item.index)
            .DefaultIfEmpty(defaultValue: -1)
            .Single();

    private IReadOnlyList<TableIndexBinding> CurrentIndexes(TableStorageContext tableStorageContext)
    {
        var indexes = tableStorageContext.Indexes!;

        if (indexes.Any(predicate: index => index.Definition!.TableId != tableStorageContext.Definition!.Id))
        {
            throw new InvalidOperationException(message: "The current index provider returned an index for another table.");
        }

        return indexes;
    }

    private async ValueTask<TableDeleteResult> RemoveLogicalTableRowAsync(TableStorageContext tableStorageContext, RowId rowId, CancellationToken cancellationToken = default)
    {
        await heapIndexMutations[tableStorageContext.MutationId].PrepareHeapIndexMutationContextWriteAsync(heapIndexMutationContext: new HeapIndexMutationContext { RowId = rowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var current = await ReadHeapBytesAsync(tableStorageContext: tableStorageContext, rowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (current.Result != TableHeapLookupResult.Found)
        {
            return new TableDeleteResult
            {
                Deleted = false,
                DeferredCleanupPageIds = Array.Empty<PageId>()
            };
        }

        var row = await DecodeTableRowAsync(tableStorageContext: tableStorageContext, source: current.Row, tableDefinition: tableStorageContext.Schema!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var overflowReferences = ReadTableOverflowReferences(tableStorageContext: tableStorageContext, row: current.Row.Span, tableDefinition: tableStorageContext.Schema!)
            .Values.ToArray();

        List<(TableIndexBinding Index, sql_storage_engine.Models.Indexes.IndexKey Key, byte[] Payload)> removed = [];
        var indexes = CurrentIndexes(tableStorageContext: tableStorageContext);

        try
        {
            foreach (var index in indexes)
            {
                foreach (sql_storage_engine.Models.Indexes.IndexKey key in EncodeIndexKeys(row: row, catalogTable: tableStorageContext.Definition!, catalogIndex: index.Definition!))
                {
                    if (!await RemoveIndexReferenceAsync(tableStorageContext: tableStorageContext, tableIndexBinding: index, indexKey: key, rowId: rowId, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
                    {
                        throw new StorageCorruptionException(message: $"Index {index.Definition!.Id} is missing the row being deleted.");
                    }

                    removed.Add(item: (index, key, EncodeIndexIncludedValues(row: row, catalogTable: tableStorageContext.Definition!, catalogIndex: index.Definition!)));
                }
            }

            if (!await RemoveHeapBytesAsync(tableStorageContext: tableStorageContext, deletedRowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
            {
                throw new StorageCorruptionException(message: "Heap row became inaccessible during coordinated deletion.");
            }
        }
        catch (Exception exception)
        {
            List<PageId> unreclaimed = [];

            foreach (var mutation in removed.AsEnumerable()
            .Reverse())
            {
                try
                {
                    await PersistIndexReferenceAsync(tableStorageContext: tableStorageContext, tableIndexBinding: mutation.Index, newIndexKey: mutation.Key, newRowId: rowId, includedPayload: mutation.Payload, token: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (StorageException)
                {
                    unreclaimed.Add(item: mutation.Index.Definition!.RootPageId);
                }
            }

            try
            {
                await PublishCatalogIndexRootsAsync(cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (StorageException)
            {
                unreclaimed.AddRange(collection: indexes.Select(selector: index => index.Definition!.RootPageId));
            }

            throw new TableMutationException(message: "Table deletion failed before the heap row was removed; index compensation was attempted.", unreclaimedPageIds: unreclaimed.Distinct()
            .ToArray(), innerException: exception);
        }

        List<PageId> deferred = [];

        foreach (var reference in overflowReferences)
        {
            try
            {
                await FreeTableOverflowAsync(tableStorageContext: tableStorageContext, overflowReference: reference, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (Exception)
            {
                deferred.Add(item: reference.FirstPageId);
            }
        }

        await PublishCatalogIndexRootsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new TableDeleteResult
        {
            Deleted = true,
            DeferredCleanupPageIds = deferred.AsReadOnly()
        };
    }
}