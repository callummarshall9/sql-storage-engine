namespace sql_storage_engine.Rows;

public readonly record struct ColumnUpdate(int ColumnIndex, SqlValue Value);

public sealed class RowUpdate
{
    private readonly ColumnUpdate[] _columns;
    public RowUpdate(IEnumerable<ColumnUpdate> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns.ToArray();
    }
    public IReadOnlyList<ColumnUpdate> Columns => Array.AsReadOnly(_columns);
}
