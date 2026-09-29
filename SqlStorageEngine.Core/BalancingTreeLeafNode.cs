namespace sql_storage_engine;

public sealed class BalancingTreeLeafNode<TKey, TValue> : BalancingTreeNode<TKey, TValue>
{
    public override IReadOnlyList<TKey> Keys => MutableEntries.Select(entry => entry.Key).ToList();
    public IReadOnlyList<BTreeEntry<TKey, TValue>> Entries => MutableEntries;
    public override IReadOnlyList<BalancingTreeNode<TKey, TValue>> Children => [];
    public BalancingTreeLeafNode<TKey, TValue>? Previous { get; internal set; }
    public BalancingTreeLeafNode<TKey, TValue>? Next { get; internal set; }

    internal List<BTreeEntry<TKey, TValue>> MutableEntries { get; set; } = [];
}
