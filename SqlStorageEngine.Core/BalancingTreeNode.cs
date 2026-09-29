namespace sql_storage_engine;

public abstract class BalancingTreeNode<TKey, TValue>
{
    public BalancingTreeInternalNode<TKey, TValue>? Parent { get; internal set; }
    public abstract IReadOnlyList<TKey> Keys { get; }
    public abstract IReadOnlyList<BalancingTreeNode<TKey, TValue>> Children { get; }
    public bool IsLeaf => this is BalancingTreeLeafNode<TKey, TValue>;
}
