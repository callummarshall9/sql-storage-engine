using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine;

internal static class GraphStorageRuntime
{
    internal static void Validate(GraphNodeId nodeId, DatabaseId databaseId, CatalogTable table,
        string parameterName)
    {
        if (nodeId.DatabaseId != databaseId || nodeId.TableId != table.Id ||
            nodeId.SchemaVersion != table.SchemaVersion)
            throw new ArgumentException("The graph node identity does not belong to the current database/table/schema.",
                parameterName);
    }

    internal static void Validate(GraphEdgeId edgeId, DatabaseId databaseId, CatalogTable table,
        string parameterName)
    {
        if (edgeId.DatabaseId != databaseId || edgeId.TableId != table.Id ||
            edgeId.SchemaVersion != table.SchemaVersion)
            throw new ArgumentException("The graph edge identity does not belong to the current database/table/schema.",
                parameterName);
    }

    internal static async ValueTask<RowId?> FindUniqueAsync(CatalogService catalog, CatalogTable table,
        IndexId indexId, SqlValue identity, CancellationToken cancellationToken)
    {
        var index = catalog.Indexes.Single(candidate => candidate.Id == indexId);
        var key = CatalogIndexKey.EncodeValues([identity], table, index);
        var matches = await catalog.OpenIndex(index).FindAsync(key, cancellationToken).ConfigureAwait(false);
        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new StorageCorruptionException("A unique graph identity index returned duplicate rows.")
        };
    }

    internal static Row Append(Row payload, params SqlValue[] generated)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new Row(payload.Values.Concat(generated));
    }

    internal static Row Payload(Row stored, int generatedCount) =>
        new(stored.Values.Take(stored.Values.Count - generatedCount));

    internal static void ValidatePayload(Row row, CatalogTable table, int generatedCount, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(row);
        var expected = table.Columns.Count - generatedCount;
        if (row.Values.Count != expected)
            throw new ArgumentException($"The graph payload requires exactly {expected} values.", parameterName);
    }

    internal static void ValidatePayloadUpdate(RowUpdate update, int payloadColumnCount)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (update.Columns.Any(column => column.ColumnIndex < 0 || column.ColumnIndex >= payloadColumnCount))
            throw new ArgumentException("Graph identity and endpoint columns cannot be updated as payload.", nameof(update));
    }

    internal static int Compare(RowId left, RowId right)
    {
        var page = left.PageId.Value.CompareTo(right.PageId.Value);
        if (page != 0) return page;
        var slot = left.SlotId.Value.CompareTo(right.SlotId.Value);
        return slot != 0 ? slot : left.Generation.Value.CompareTo(right.Generation.Value);
    }

    internal static IndexRange ExactRange(CatalogTable table, CatalogIndex index, SqlValue value)
    {
        var key = CatalogIndexKey.EncodeValues([value], table, index);
        return new IndexRange(key, key, true, true);
    }
}
