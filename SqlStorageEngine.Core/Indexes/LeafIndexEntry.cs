using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Indexes;

public readonly record struct LeafIndexEntry
{
    private readonly byte[] _payload;
    public LeafIndexEntry(IndexKey key, RowId rowId) : this(key, rowId, ReadOnlyMemory<byte>.Empty) { }
    public LeafIndexEntry(IndexKey key, RowId rowId, ReadOnlyMemory<byte> includedPayload)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        RowId = rowId;
        _payload = includedPayload.ToArray();
    }
    public IndexKey Key { get; }
    public RowId RowId { get; }
    public ReadOnlyMemory<byte> IncludedPayload => _payload ?? [];
    public bool Equals(LeafIndexEntry other) => Key.Equals(other.Key) && RowId == other.RowId &&
        IncludedPayload.Span.SequenceEqual(other.IncludedPayload.Span);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Key); hash.Add(RowId);
        foreach (var value in IncludedPayload.Span) hash.Add(value);
        return hash.ToHashCode();
    }
}
