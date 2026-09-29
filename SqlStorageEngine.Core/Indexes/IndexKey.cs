namespace sql_storage_engine.Indexes;

/// <summary>An immutable, lexicographically ordered binary index key.</summary>
public sealed class IndexKey : IComparable<IndexKey>, IEquatable<IndexKey>
{
    private readonly byte[] _bytes;
    public IndexKey(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) throw new ArgumentException("Index keys cannot be empty.", nameof(bytes));
        if (bytes.Length > ushort.MaxValue) throw new ArgumentException("Index key exceeds 65,535 bytes.", nameof(bytes));
        _bytes = bytes.ToArray();
    }
    public ReadOnlyMemory<byte> Bytes => _bytes.ToArray();
    public int CompareTo(IndexKey? other) => other is null ? 1 : _bytes.AsSpan().SequenceCompareTo(other._bytes);
    public bool Equals(IndexKey? other) => other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);
    public override bool Equals(object? obj) => obj is IndexKey other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _bytes) hash.Add(value);
        return hash.ToHashCode();
    }
    public override string ToString() => Convert.ToHexString(_bytes);
}
