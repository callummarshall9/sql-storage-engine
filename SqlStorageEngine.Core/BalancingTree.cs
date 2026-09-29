namespace sql_storage_engine;

public class BalancingTree<TKey, TValue>
{
    public BalancingTreeNode<TKey, TValue> Root { get; internal set; } =
        new BalancingTreeLeafNode<TKey, TValue>();

    public int Order { get; init; }
}
