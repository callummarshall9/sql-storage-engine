namespace sql_storage_engine;

/// <summary>
/// Represents an ordered, duplicate-key B+ tree.
/// </summary>
public interface IBPlusTree<TKey, TValue>
{
    int Order { get; }
    int Count { get; }

    void Add(TKey key, TValue value);
    bool Remove(TKey key, TValue value);
    bool ContainsKey(TKey key);
    bool TryGetValue(TKey key, out TValue value);
    IEnumerable<TValue> Find(TKey key);

    bool TryGetLowerBound(TKey key, out BTreeEntry<TKey, TValue> entry);
    bool TryGetUpperBound(TKey key, out BTreeEntry<TKey, TValue> entry);

    IEnumerable<BTreeEntry<TKey, TValue>> Scan(
        ScanDirection direction = ScanDirection.Ascending);

    IEnumerable<BTreeEntry<TKey, TValue>> Scan(BTreeRange<TKey> range);
}
