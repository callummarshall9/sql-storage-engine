namespace sql_storage_engine;

public enum ScanDirection
{
    Ascending,
    Descending
}

public readonly record struct BTreeEntry<TKey, TValue>(TKey Key, TValue Value);

public sealed record BTreeRange<TKey>(
    TKey LowerBound,
    TKey UpperBound,
    bool IncludeLowerBound = true,
    bool IncludeUpperBound = true,
    ScanDirection Direction = ScanDirection.Ascending);

public class BalancingTree<TKey, TValue>
{
    public BalancingTreeNode<TKey, TValue> Root { get; internal set; } =
        new BalancingTreeLeafNode<TKey, TValue>();

    public int Order { get; init; }
}

public abstract class BalancingTreeNode<TKey, TValue>
{
    public BalancingTreeInternalNode<TKey, TValue>? Parent { get; internal set; }
    public abstract IReadOnlyList<TKey> Keys { get; }
    public abstract IReadOnlyList<BalancingTreeNode<TKey, TValue>> Children { get; }
    public bool IsLeaf => this is BalancingTreeLeafNode<TKey, TValue>;
}

public sealed class BalancingTreeLeafNode<TKey, TValue> : BalancingTreeNode<TKey, TValue>
{
    public override IReadOnlyList<TKey> Keys => MutableEntries.Select(entry => entry.Key).ToList();
    public IReadOnlyList<BTreeEntry<TKey, TValue>> Entries => MutableEntries;
    public override IReadOnlyList<BalancingTreeNode<TKey, TValue>> Children => [];
    public BalancingTreeLeafNode<TKey, TValue>? Previous { get; internal set; }
    public BalancingTreeLeafNode<TKey, TValue>? Next { get; internal set; }

    internal List<BTreeEntry<TKey, TValue>> MutableEntries { get; set; } = [];
}

public sealed class BalancingTreeInternalNode<TKey, TValue> : BalancingTreeNode<TKey, TValue>
{
    public override IReadOnlyList<TKey> Keys => MutableSeparators;
    public override IReadOnlyList<BalancingTreeNode<TKey, TValue>> Children => MutableChildren;

    // Separator i is the smallest key reachable through child i + 1.
    internal List<TKey> MutableSeparators { get; set; } = [];
    internal List<BalancingTreeNode<TKey, TValue>> MutableChildren { get; set; } = [];
}
