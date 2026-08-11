using sql_storage_engine.Catalog;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Overflow;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;

namespace sql_storage_engine.Tables;

/// <summary>Binds catalog index metadata to its persistent tree.</summary>
public sealed class TableIndex(CatalogIndex definition, PersistentBPlusTree tree)
{
    public CatalogIndex Definition { get; } = definition ?? throw new ArgumentNullException(nameof(definition));
    public PersistentBPlusTree Tree { get; } = tree ?? throw new ArgumentNullException(nameof(tree));
    public int AddCount { get; private set; }
    public int RemoveCount { get; private set; }
    internal async ValueTask AddAsync(IndexKey key, RowId rowId, CancellationToken token)
    { await Tree.InsertAsync(key, rowId, token).ConfigureAwait(false); AddCount++; }
    internal async ValueTask<bool> RemoveAsync(IndexKey key, RowId rowId, CancellationToken token = default)
    { var removed = await Tree.RemoveAsync(key, rowId, token).ConfigureAwait(false); if (removed) RemoveCount++; return removed; }
}

/// <summary>Describes a successful logical row update and whether its physical identifier changed.</summary>
public sealed record TableUpdateResult(bool Updated, RowId PreviousRowId, RowId CurrentRowId)
{
    public bool Relocated => PreviousRowId != CurrentRowId;
}

/// <summary>Describes logical deletion and overflow roots that require deferred reclamation.</summary>
public sealed record TableDeleteResult(bool Deleted, IReadOnlyList<PageId> DeferredCleanupPageIds);

/// <summary>Reports whether a row was inserted or skipped by a unique index using IGNORE_DUP_KEY.</summary>
public sealed record TableInsertResult(bool Inserted, RowId? RowId, IReadOnlyList<string> Warnings);

public sealed class DuplicateKeyIgnoredException : InvalidOperationException
{
    public DuplicateKeyIgnoredException(string message) : base(message) { }
}

/// <summary>Reports a failed logical table mutation and storage roots requiring deferred cleanup.</summary>
public sealed class TableMutationException : StorageException
{
    public TableMutationException(string message, IReadOnlyList<PageId> unreclaimedPageIds, Exception innerException)
        : base(message, innerException) => UnreclaimedPageIds = unreclaimedPageIds.ToArray();
    public IReadOnlyList<PageId> UnreclaimedPageIds { get; }
}

/// <summary>Coordinates logical rows with their heap, overflow values, and all secondary indexes.</summary>
public sealed class TableStorage
{
    private readonly CatalogTable _table;
    private readonly TableDefinition _schema;
    private readonly TableHeap _heap;
    private readonly OverflowRowCodec _rowCodec;
    private readonly OverflowManager _overflow;
    private readonly TableIndex[] _indexes;
    private readonly Func<IReadOnlyList<TableIndex>>? _indexProvider;
    private readonly Func<CancellationToken, ValueTask<SqlValue>>? _rowVersionGenerator;
    private readonly Func<CancellationToken, ValueTask<SqlValue>>? _identityGenerator;
    private readonly Func<CatalogGeneratedAlwaysKind, CancellationToken, ValueTask<SqlValue>>? _generatedValueGenerator;

    public TableStorage(CatalogTable table, TableHeap heap, OverflowRowCodec rowCodec,
        OverflowManager overflow, IEnumerable<TableIndex> indexes,
        Func<CancellationToken, ValueTask<SqlValue>>? rowVersionGenerator = null,
        Func<CancellationToken, ValueTask<SqlValue>>? identityGenerator = null,
        Func<CatalogGeneratedAlwaysKind, CancellationToken, ValueTask<SqlValue>>? generatedValueGenerator = null,
        Func<IReadOnlyList<TableIndex>>? indexProvider = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(heap);
        ArgumentNullException.ThrowIfNull(rowCodec);
        ArgumentNullException.ThrowIfNull(overflow);
        ArgumentNullException.ThrowIfNull(indexes);
        _indexes = indexes.ToArray();
        if (_indexes.Any(index => index.Definition.TableId != table.Id))
            throw new ArgumentException("Every index must belong to the table.", nameof(indexes));
        _table = table;
        _schema = CatalogService.ToRowTable(table);
        _heap = heap;
        _rowCodec = rowCodec;
        _overflow = overflow;
        _rowVersionGenerator = rowVersionGenerator;
        _identityGenerator = identityGenerator;
        _generatedValueGenerator = generatedValueGenerator;
        _indexProvider = indexProvider;
    }

    public CatalogTable Definition => _table;

    /// <summary>Streams a stable logical projection of every live heap row.</summary>
    public async IAsyncEnumerable<StoredRow> ScanAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var entry in _heap.ScanAsync(cancellationToken).ConfigureAwait(false))
            yield return new StoredRow(entry.RowId, ProjectColumnSet(
                await _rowCodec.DecodeAsync(entry.Row, _schema, cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>Validates and inserts one logical row into the heap and every published index.</summary>
    public async ValueTask<RowId> InsertAsync(Row row, CancellationToken cancellationToken = default)
    {
        var result = await TryInsertAsync(row, cancellationToken).ConfigureAwait(false);
        return result.Inserted ? result.RowId!.Value : throw new DuplicateKeyIgnoredException(result.Warnings.Single());
    }

    /// <summary>Inserts a row or reports a SQL Server IGNORE_DUP_KEY skip without manufacturing a RowId.</summary>
    public async ValueTask<TableInsertResult> TryInsertAsync(Row row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        row = await PrepareInsertAsync(row, cancellationToken).ConfigureAwait(false);
        _schema.ValidateRow(row); // No allocation or mutation occurs before complete logical validation.
        RowEncodingResult encoded = await _rowCodec.EncodeAsync(row, _schema, cancellationToken).ConfigureAwait(false);
        RowId? rowId = null;
        List<(TableIndex Index, IndexKey Key)> insertedIndexes = [];
        HashSet<OverflowReference> reclaimed = [];
        var indexes = CurrentIndexes();
        try
        {
            var storedRow = await _rowCodec.DecodeAsync(encoded.Bytes, _schema, cancellationToken).ConfigureAwait(false);
            foreach (var index in indexes.Where(candidate => candidate.Definition.IsUnique &&
                                                               candidate.Definition.IgnoreDuplicateKey))
            {
                var key = CatalogIndexKey.Encode(storedRow, _table, index.Definition);
                if ((await index.Tree.FindAsync(key, cancellationToken).ConfigureAwait(false)).Count == 0) continue;
                foreach (var reference in encoded.NewlyAllocated.Reverse())
                {
                    await _overflow.FreeAsync(reference, cancellationToken).ConfigureAwait(false);
                    reclaimed.Add(reference);
                }
                return new TableInsertResult(false, null,
                    [$"Duplicate key was ignored by index '{index.Definition.Name}'."]);
            }
            rowId = await _heap.InsertAsync(encoded.Bytes, cancellationToken).ConfigureAwait(false);
            foreach (var index in indexes)
            {
                foreach (var key in CatalogIndexKey.EncodeEntries(storedRow, _table, index.Definition))
                {
                    await index.AddAsync(key, rowId.Value, cancellationToken).ConfigureAwait(false);
                    insertedIndexes.Add((index, key));
                }
            }
            return new TableInsertResult(true, rowId.Value, []);
        }
        catch (Exception exception)
        {
            List<PageId> unreclaimed = [];
            for (var index = insertedIndexes.Count - 1; index >= 0; index--)
                try { await insertedIndexes[index].Index.RemoveAsync(insertedIndexes[index].Key, rowId!.Value).ConfigureAwait(false); }
                catch (StorageException) { unreclaimed.Add(insertedIndexes[index].Index.Definition.RootPageId); }
            if (rowId is { } insertedRow)
                try { if (!await _heap.DeleteAsync(insertedRow).ConfigureAwait(false)) unreclaimed.Add(insertedRow.PageId); }
                catch (StorageException) { unreclaimed.Add(insertedRow.PageId); }
            foreach (var reference in encoded.NewlyAllocated.Reverse())
            {
                if (reclaimed.Contains(reference)) continue;
                try { await _overflow.FreeAsync(reference).ConfigureAwait(false); }
                catch (StorageException) { unreclaimed.Add(reference.FirstPageId); }
            }
            throw new TableMutationException("Table insertion failed and compensating cleanup was attempted.",
                unreclaimed.Distinct().ToArray(), exception);
        }
    }

    /// <summary>Returns the logical row for a live generation-safe row identifier.</summary>
    public async ValueTask<(bool Found, Row? Row)> TryGetAsync(RowId rowId,
        CancellationToken cancellationToken = default)
    {
        var result = await _heap.ReadAsync(rowId, cancellationToken).ConfigureAwait(false);
        if (result.Result != TableHeapLookupResult.Found) return (false, null);
        return (true, ProjectColumnSet(
            await _rowCodec.DecodeAsync(result.Row, _schema, cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>Applies selected columns while maintaining changed keys and all RowId references after relocation.</summary>
    public async ValueTask<TableUpdateResult> UpdateAsync(RowId rowId, RowUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var current = await _heap.ReadAsync(rowId, cancellationToken).ConfigureAwait(false);
        if (current.Result != TableHeapLookupResult.Found) return new TableUpdateResult(false, rowId, rowId);
        var oldBytes = current.Row.ToArray();
        var oldRow = await _rowCodec.DecodeAsync(current.Row, _schema, cancellationToken).ConfigureAwait(false);
        update = await PrepareUpdateAsync(oldRow, update, cancellationToken).ConfigureAwait(false);
        var replacement = await _rowCodec.ApplyUpdateAsync(current.Row, update, _schema, cancellationToken).ConfigureAwait(false);
        var newRow = await _rowCodec.DecodeAsync(replacement.Bytes, _schema, cancellationToken).ConfigureAwait(false);
        var heapResult = await _heap.UpdateAsync(rowId, replacement.Bytes, cancellationToken).ConfigureAwait(false);
        RowId newRowId = rowId;
        var relocated = heapResult == HeapUpdateResult.RelocationRequired;
        if (relocated) newRowId = await _heap.InsertAsync(replacement.Bytes, cancellationToken).ConfigureAwait(false);
        else if (heapResult != HeapUpdateResult.Updated)
        {
            foreach (var reference in replacement.NewlyAllocated) await _overflow.FreeAsync(reference).ConfigureAwait(false);
            return new TableUpdateResult(false, rowId, rowId);
        }

        List<(TableIndex Index, IndexKey Key)> removedEntries = [];
        List<(TableIndex Index, IndexKey Key)> addedEntries = [];
        var indexes = CurrentIndexes();
        try
        {
            foreach (var index in indexes)
            {
                var oldKeys = CatalogIndexKey.EncodeEntries(oldRow, _table, index.Definition).ToHashSet();
                var newKeys = CatalogIndexKey.EncodeEntries(newRow, _table, index.Definition).ToHashSet();
                var remove = relocated ? oldKeys : oldKeys.Except(newKeys);
                var add = relocated ? newKeys : newKeys.Except(oldKeys);
                foreach (var key in remove)
                {
                    if (!await index.RemoveAsync(key, rowId, cancellationToken).ConfigureAwait(false))
                        throw new StorageCorruptionException($"Index {index.Definition.Id} is missing the row being updated.");
                    removedEntries.Add((index, key));
                }
                foreach (var key in add)
                {
                    await index.AddAsync(key, newRowId, cancellationToken).ConfigureAwait(false);
                    addedEntries.Add((index, key));
                }
            }
            if (relocated && !await _heap.DeleteAsync(rowId, cancellationToken).ConfigureAwait(false))
                throw new StorageCorruptionException("Relocated row's previous heap slot could not be deleted.");
            foreach (var reference in replacement.Retired) await _overflow.FreeAsync(reference, cancellationToken).ConfigureAwait(false);
            return new TableUpdateResult(true, rowId, newRowId);
        }
        catch (Exception exception)
        {
            List<PageId> unreclaimed = [];
            for (var index = addedEntries.Count - 1; index >= 0; index--)
            {
                try
                { await addedEntries[index].Index.RemoveAsync(addedEntries[index].Key, newRowId).ConfigureAwait(false); }
                catch (StorageException) { unreclaimed.Add(addedEntries[index].Index.Definition.RootPageId); }
            }
            for (var index = removedEntries.Count - 1; index >= 0; index--)
            {
                try
                { await removedEntries[index].Index.AddAsync(removedEntries[index].Key, rowId, CancellationToken.None).ConfigureAwait(false); }
                catch (StorageException) { unreclaimed.Add(removedEntries[index].Index.Definition.RootPageId); }
            }
            try
            {
                if (relocated) await _heap.DeleteAsync(newRowId).ConfigureAwait(false);
                else if (await _heap.UpdateAsync(rowId, oldBytes).ConfigureAwait(false) != HeapUpdateResult.Updated)
                    unreclaimed.Add(rowId.PageId);
            }
            catch (StorageException) { unreclaimed.Add(newRowId.PageId); }
            foreach (var reference in replacement.NewlyAllocated)
                try { await _overflow.FreeAsync(reference).ConfigureAwait(false); }
                catch (StorageException) { unreclaimed.Add(reference.FirstPageId); }
            throw new TableMutationException("Table update failed and the previous logical state was restored.",
                unreclaimed.Distinct().ToArray(), exception);
        }
    }

    private async ValueTask<Row> PrepareInsertAsync(Row row, CancellationToken cancellationToken)
    {
        if (row.Values.Count != _schema.Columns.Count)
            throw new ArgumentException("Row width does not match schema width.", nameof(row));
        var values = row.Values.ToArray();
        SqlValue? suppliedColumnSet = null;
        var context = new Dictionary<string, SqlValue>(StringComparer.OrdinalIgnoreCase);
        for (var position = 0; position < _table.Columns.Count; position++)
        {
            var column = _table.Columns[position]; var supplied = values[position];
            if (column.Identity is not null)
            {
                if (supplied is not DefaultSqlValue && !supplied.IsNull)
                    throw new ArgumentException($"IDENTITY column '{column.Name}' cannot be assigned explicitly.", nameof(row));
                values[position] = await RequireIdentityGenerator()(cancellationToken).ConfigureAwait(false);
            }
            else if (column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp)
                values[position] = await RequireRowVersionGenerator()(cancellationToken).ConfigureAwait(false);
            else if (column.GeneratedAlways != CatalogGeneratedAlwaysKind.None)
            {
                if (supplied is not DefaultSqlValue && !supplied.IsNull)
                    throw new ArgumentException($"Generated-always column '{column.Name}' cannot be assigned explicitly.", nameof(row));
                values[position] = SqlConversion.ConvertTo(column.Type,
                    await RequireGeneratedValueGenerator()(column.GeneratedAlways, cancellationToken).ConfigureAwait(false));
            }
            else if (column.IsComputed)
            {
                if (supplied is not DefaultSqlValue && !supplied.IsNull)
                    throw new ArgumentException($"Computed column '{column.Name}' cannot be assigned explicitly.", nameof(row));
                values[position] = SqlValue.Default;
            }
            else if (column.IsColumnSet)
            {
                suppliedColumnSet = supplied;
                values[position] = SqlValue.Null;
            }
            else
            {
                if (supplied is DefaultSqlValue)
                    supplied = column.DefaultExpression is null ? SqlValue.Null : SqlExpressions.Evaluate(column.DefaultExpression, context);
                values[position] = supplied.IsNull ? supplied : SqlConversion.ConvertTo(column.Type, supplied);
            }
            if (values[position] is not DefaultSqlValue) context[column.Name] = values[position];
        }
        if (suppliedColumnSet is not null and not DefaultSqlValue && !suppliedColumnSet.IsNull)
        {
            if (_table.Columns.Select((column, index) => (column, index)).Any(item =>
                    item.column.IsSparse && !row.Values[item.index].IsNull && row.Values[item.index] is not DefaultSqlValue))
                throw new ArgumentException("Assign either individual SPARSE columns or the XML column set, not both.", nameof(row));
            ApplyColumnSet(values, suppliedColumnSet, context, clearExisting: true);
        }
        ComputeColumns(values, context); ClearColumnSet(values, context);
        var result = new Row(values); _schema.ValidateRow(result); ValidateChecks(result); return result;
    }

    private async ValueTask<RowUpdate> PrepareUpdateAsync(Row oldRow, RowUpdate update, CancellationToken cancellationToken)
    {
        if (update.Columns.Select(column => column.ColumnIndex).Distinct().Count() != update.Columns.Count)
            throw new ArgumentException("Updated column indexes must be unique.", nameof(update));
        var values = oldRow.Values.ToArray();
        var context = _table.Columns.Select((column, index) => (column.Name, Value: values[index]))
            .ToDictionary(item => item.Name, item => item.Value, StringComparer.OrdinalIgnoreCase);
        ColumnUpdate? columnSetUpdate = update.Columns.Where(changed =>
                changed.ColumnIndex >= 0 && changed.ColumnIndex < _table.Columns.Count &&
                _table.Columns[changed.ColumnIndex].IsColumnSet)
            .Select(changed => (ColumnUpdate?)changed).SingleOrDefault();
        if (columnSetUpdate is not null && update.Columns.Any(changed =>
                changed.ColumnIndex >= 0 && changed.ColumnIndex < _table.Columns.Count &&
                _table.Columns[changed.ColumnIndex].IsSparse))
            throw new ArgumentException("Update either individual SPARSE columns or the XML column set, not both.", nameof(update));
        foreach (var changed in update.Columns)
        {
            if (changed.ColumnIndex < 0 || changed.ColumnIndex >= _table.Columns.Count)
                throw new ArgumentOutOfRangeException(nameof(update), "Updated column index is outside the schema.");
            var column = _table.Columns[changed.ColumnIndex];
            if (column.Identity is not null) throw new ArgumentException("An IDENTITY column cannot be updated.", nameof(update));
            if (column.IsComputed) throw new ArgumentException("A computed column cannot be updated directly.", nameof(update));
            if (column.IsColumnSet) continue;
            if (column.GeneratedAlways != CatalogGeneratedAlwaysKind.None)
                throw new ArgumentException("A generated-always column cannot be updated directly.", nameof(update));
            if (column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp)
                throw new ArgumentException("A rowversion column cannot be assigned explicitly.", nameof(update));
            var value = changed.Value;
            if (value is DefaultSqlValue)
                value = column.DefaultExpression is null ? SqlValue.Null : SqlExpressions.Evaluate(column.DefaultExpression, context);
            values[changed.ColumnIndex] = value.IsNull ? value : SqlConversion.ConvertTo(column.Type, value);
            context[column.Name] = values[changed.ColumnIndex];
        }
        if (columnSetUpdate is not null)
            ApplyColumnSet(values, columnSetUpdate.Value.Value, context, clearExisting: true);
        var rowVersionPosition = GetRowVersionPosition();
        if (rowVersionPosition >= 0)
        {
            values[rowVersionPosition] = await RequireRowVersionGenerator()(cancellationToken).ConfigureAwait(false);
            context[_table.Columns[rowVersionPosition].Name] = values[rowVersionPosition];
        }
        for (var position = 0; position < _table.Columns.Count; position++)
        {
            var column = _table.Columns[position];
            if (column.GeneratedAlways is CatalogGeneratedAlwaysKind.None or CatalogGeneratedAlwaysKind.RowEnd) continue;
            values[position] = SqlConversion.ConvertTo(column.Type,
                await RequireGeneratedValueGenerator()(column.GeneratedAlways, cancellationToken).ConfigureAwait(false));
            context[column.Name] = values[position];
        }
        ComputeColumns(values, context); ClearColumnSet(values, context);
        var result = new Row(values); _schema.ValidateRow(result); ValidateChecks(result);
        return new RowUpdate(values.Select((value, index) => (value, index))
            .Where(item => !Equals(item.value, oldRow.Values[item.index]))
            .Select(item => new ColumnUpdate(item.index, item.value)));
    }

    private void ComputeColumns(SqlValue[] values, IDictionary<string, SqlValue> context)
    {
        foreach (var position in CatalogTable.GetComputedColumnOrder(_table.Columns))
        {
            var column = _table.Columns[position];
            var computed = SqlExpressions.Evaluate(column.ComputedExpression!, (IReadOnlyDictionary<string, SqlValue>)context);
            var source = CatalogTable.ResolveDirectColumnReference(column.ComputedExpression!, _table.Columns);
            values[position] = computed.IsNull ? computed : source is null
                ? SqlConversion.ConvertTo(column.Type, computed)
                : SqlConversion.ConvertTo(column.Type, source.Type, computed);
            context[column.Name] = values[position];
        }
    }

    private Row ProjectColumnSet(Row row)
    {
        var position = _table.Columns.Select((column, index) => (column, index))
            .Where(item => item.column.IsColumnSet).Select(item => item.index).DefaultIfEmpty(-1).Single();
        if (position < 0) return row;
        var values = row.Values.ToArray();
        var elements = _table.Columns.Select((column, index) => (column, index)).Where(item => item.column.IsSparse && !values[item.index].IsNull)
            .Select(item => new XElement(XmlConvert.EncodeName(item.column.Name),
                ((TextSqlValue)SqlConversion.ConvertTo(SqlType.NVarCharMax(), values[item.index])).Value));
        values[position] = SqlValue.Text(string.Concat(elements.Select(element =>
            element.ToString(SaveOptions.DisableFormatting))));
        return new Row(values);
    }

    private void ApplyColumnSet(SqlValue[] values, SqlValue supplied,
        IDictionary<string, SqlValue> context, bool clearExisting)
    {
        if (clearExisting)
            foreach (var item in _table.Columns.Select((column, index) => (column, index)).Where(item => item.column.IsSparse))
            {
                values[item.index] = SqlValue.Null;
                context[item.column.Name] = SqlValue.Null;
            }
        if (supplied.IsNull) return;
        var xml = ((TextSqlValue)SqlConversion.ConvertTo(SqlType.Xml, supplied)).Value;
        XDocument document;
        try { document = XDocument.Parse("<columnSet>" + xml + "</columnSet>", LoadOptions.PreserveWhitespace); }
        catch (System.Xml.XmlException exception)
        { throw new ArgumentException("The XML column set is not well formed.", nameof(supplied), exception); }
        var sparseByName = _table.Columns.Select((column, index) => (column, index)).Where(item => item.column.IsSparse)
            .ToDictionary(item => item.column.Name, item => item, StringComparer.Ordinal);
        HashSet<string> assigned = new(StringComparer.Ordinal);
        foreach (var element in document.Root!.Elements())
        {
            if (element.Name.NamespaceName.Length != 0 || !sparseByName.TryGetValue(XmlConvert.DecodeName(element.Name.LocalName), out var item))
                throw new ArgumentException($"Column-set element '{element.Name}' does not identify a SPARSE column.", nameof(supplied));
            if (!assigned.Add(item.column.Name))
                throw new ArgumentException($"SPARSE column '{item.column.Name}' occurs more than once in the column set.", nameof(supplied));
            var nil = element.Attribute(XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance") + "nil")?.Value;
            SqlValue value = string.Equals(nil, "true", StringComparison.OrdinalIgnoreCase) || nil == "1"
                ? SqlValue.Null
                : SqlConversion.ConvertTo(item.column.Type, SqlValue.Text(NormalizeColumnSetText(item.column.Type, element.Value)));
            values[item.index] = value;
            context[item.column.Name] = value;
        }
    }

    private void ClearColumnSet(SqlValue[] values, IDictionary<string, SqlValue> context)
    {
        var position = _table.Columns.Select((column, index) => (column, index))
            .Where(item => item.column.IsColumnSet).Select(item => item.index).DefaultIfEmpty(-1).Single();
        if (position < 0) return;
        values[position] = SqlValue.Null;
        context[_table.Columns[position].Name] = SqlValue.Null;
    }

    private static string NormalizeColumnSetText(SqlType type, string value)
    {
        if (value.Length != 0) return value;
        return type.Name is SqlTypeName.Bit or SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or
            SqlTypeName.BigInt or SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.Real or SqlTypeName.Float or
            SqlTypeName.Money or SqlTypeName.SmallMoney ? "0" : value;
    }

    private void ValidateChecks(Row row)
    {
        if (_table.CheckConstraints.Count == 0) return;
        var context = _table.Columns.Select((column, index) => (column.Name, Value: row.Values[index]))
            .ToDictionary(item => item.Name, item => item.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var check in _table.CheckConstraints)
        {
            var result = SqlExpressions.Evaluate(check.Expression, context);
            if (!result.IsNull && !((BooleanSqlValue)SqlConversion.ConvertTo(SqlType.Bit, result)).Value)
                throw new ArgumentException($"CHECK constraint '{check.Name}' rejected the row.", nameof(row));
        }
    }

    private int GetRowVersionPosition() => _table.Columns.Select((column, index) => (column, index))
        .Where(item => item.column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp)
        .Select(item => item.index).DefaultIfEmpty(-1).Single();

    private Func<CancellationToken, ValueTask<SqlValue>> RequireRowVersionGenerator() => _rowVersionGenerator ??
        throw new InvalidOperationException("This table requires a database rowversion allocator.");
    private Func<CancellationToken, ValueTask<SqlValue>> RequireIdentityGenerator() => _identityGenerator ??
        throw new InvalidOperationException("This table requires a durable identity allocator.");
    private Func<CatalogGeneratedAlwaysKind, CancellationToken, ValueTask<SqlValue>> RequireGeneratedValueGenerator() =>
        _generatedValueGenerator ?? throw new InvalidOperationException("This table requires a generated-value allocator.");

    private IReadOnlyList<TableIndex> CurrentIndexes()
    {
        var indexes = _indexProvider?.Invoke() ?? _indexes;
        if (indexes.Any(index => index.Definition.TableId != _table.Id))
            throw new InvalidOperationException("The current index provider returned an index for another table.");
        return indexes;
    }

    /// <summary>Removes every index reference, invalidates the heap slot, and reclaims owned overflow chains.</summary>
    public async ValueTask<TableDeleteResult> DeleteAsync(RowId rowId,
        CancellationToken cancellationToken = default)
    {
        var current = await _heap.ReadAsync(rowId, cancellationToken).ConfigureAwait(false);
        if (current.Result != TableHeapLookupResult.Found)
            return new TableDeleteResult(false, Array.Empty<PageId>());
        var row = await _rowCodec.DecodeAsync(current.Row, _schema, cancellationToken).ConfigureAwait(false);
        var overflowReferences = _rowCodec.GetOverflowReferences(current.Row.Span, _schema).Values.ToArray();
        List<(TableIndex Index, IndexKey Key)> removed = [];
        var indexes = CurrentIndexes();
        try
        {
            foreach (var index in indexes)
            {
                foreach (var key in CatalogIndexKey.EncodeEntries(row, _table, index.Definition))
                {
                    if (!await index.RemoveAsync(key, rowId, cancellationToken).ConfigureAwait(false))
                        throw new StorageCorruptionException($"Index {index.Definition.Id} is missing the row being deleted.");
                    removed.Add((index, key));
                }
            }
            if (!await _heap.DeleteAsync(rowId, cancellationToken).ConfigureAwait(false))
                throw new StorageCorruptionException("Heap row became inaccessible during coordinated deletion.");
        }
        catch (Exception exception)
        {
            List<PageId> unreclaimed = [];
            foreach (var mutation in removed.AsEnumerable().Reverse())
                try { await mutation.Index.AddAsync(mutation.Key, rowId, CancellationToken.None).ConfigureAwait(false); }
                catch (StorageException) { unreclaimed.Add(mutation.Index.Definition.RootPageId); }
            throw new TableMutationException("Table deletion failed before the heap row was removed; index compensation was attempted.",
                unreclaimed.Distinct().ToArray(), exception);
        }

        List<PageId> deferred = [];
        foreach (var reference in overflowReferences)
            try { await _overflow.FreeAsync(reference, cancellationToken).ConfigureAwait(false); }
            catch (Exception) { deferred.Add(reference.FirstPageId); }
        return new TableDeleteResult(true, deferred.AsReadOnly());
    }
}
