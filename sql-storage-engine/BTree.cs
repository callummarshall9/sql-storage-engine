namespace sql_storage_engine;

public class BalancingTreeService<TKey, TValue> : IBPlusTree<TKey, TValue>
{
    private readonly IComparer<TKey> _keyComparer;
    private readonly IEqualityComparer<TValue> _valueComparer;
    private readonly int _maxEntries;
    private readonly int _minLeafEntries;
    private readonly int _minInternalChildren;
    private int _count;

    public BalancingTreeService(
        int order,
        IComparer<TKey>? keyComparer = null,
        IEqualityComparer<TValue>? valueComparer = null)
    {
        if (order < 3)
            throw new ArgumentOutOfRangeException(nameof(order), order, "Order must be at least three.");

        _keyComparer = keyComparer ?? Comparer<TKey>.Default;
        _valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
        _maxEntries = order - 1;
        _minLeafEntries = (_maxEntries + 1) / 2;
        _minInternalChildren = (order + 1) / 2;
        BalancingTree = new BalancingTree<TKey, TValue> { Order = order };
    }

    public BalancingTree<TKey, TValue> BalancingTree { get; }
    public int Order => BalancingTree.Order;
    public int Count => _count;

    public void Add(TKey key, TValue value)
    {
        var leaf = FindLeafForInsertion(key);
        var insertIndex = FindFirstEntryAfterKey(leaf.MutableEntries, key);
        leaf.MutableEntries.Insert(insertIndex, new BTreeEntry<TKey, TValue>(key, value));
        _count++;

        if (leaf.MutableEntries.Count > _maxEntries)
            SplitLeaf(leaf);

        RefreshAncestors(leaf);
    }

    public bool Remove(TKey key, TValue value)
    {
        var leaf = FindFirstCandidateLeaf(key);

        while (leaf != null)
        {
            var entryIndex = FindFirstEntryAtKey(leaf.MutableEntries, key);

            while (entryIndex < leaf.MutableEntries.Count &&
                   KeysEqual(leaf.MutableEntries[entryIndex].Key, key))
            {
                if (_valueComparer.Equals(leaf.MutableEntries[entryIndex].Value, value))
                {
                    leaf.MutableEntries.RemoveAt(entryIndex);
                    _count--;
                    RebalanceLeaf(leaf);

                    if (_count > 0)
                        RefreshAncestors(leaf);

                    return true;
                }

                entryIndex++;
            }

            if (leaf.MutableEntries.Count > 0 &&
                _keyComparer.Compare(leaf.MutableEntries[0].Key, key) > 0)
                break;

            leaf = leaf.Next;
        }

        return false;
    }

    public bool ContainsKey(TKey key)
        => TryGetValue(key, out _);

    public bool TryGetValue(TKey key, out TValue value)
    {
        foreach (var matchingValue in Find(key))
        {
            value = matchingValue;
            return true;
        }

        value = default!;
        return false;
    }

    public IEnumerable<TValue> Find(TKey key)
    {
        var leaf = FindFirstCandidateLeaf(key);

        while (leaf != null)
        {
            var entryIndex = FindFirstEntryAtKey(leaf.MutableEntries, key);

            while (entryIndex < leaf.MutableEntries.Count &&
                   KeysEqual(leaf.MutableEntries[entryIndex].Key, key))
            {
                yield return leaf.MutableEntries[entryIndex].Value;
                entryIndex++;
            }

            if (leaf.MutableEntries.Count > 0 &&
                _keyComparer.Compare(leaf.MutableEntries[^1].Key, key) > 0)
                yield break;

            leaf = leaf.Next;
        }
    }

    public bool TryGetLowerBound(TKey key, out BTreeEntry<TKey, TValue> entry)
    {
        var leaf = FindFirstCandidateLeaf(key);

        while (leaf != null)
        {
            var entryIndex = FindFirstEntryAtKey(leaf.MutableEntries, key);

            if (entryIndex < leaf.MutableEntries.Count)
            {
                entry = leaf.MutableEntries[entryIndex];
                return true;
            }

            leaf = leaf.Next;
        }

        entry = default;
        return false;
    }

    public bool TryGetUpperBound(TKey key, out BTreeEntry<TKey, TValue> entry)
    {
        var leaf = FindLastCandidateLeaf(key);

        while (leaf != null)
        {
            var entryIndex = FindFirstEntryAfterKey(leaf.MutableEntries, key);

            if (entryIndex < leaf.MutableEntries.Count)
            {
                entry = leaf.MutableEntries[entryIndex];
                return true;
            }

            leaf = leaf.Next;
        }

        entry = default;
        return false;
    }

    public IEnumerable<BTreeEntry<TKey, TValue>> Scan(
        ScanDirection direction = ScanDirection.Ascending)
        => direction == ScanDirection.Descending ? ScanDescending() : ScanAscending();

    public IEnumerable<BTreeEntry<TKey, TValue>> Scan(BTreeRange<TKey> range)
    {
        if (_keyComparer.Compare(range.LowerBound, range.UpperBound) > 0)
            yield break;

        var entries = range.Direction == ScanDirection.Descending
            ? ScanDescending()
            : ScanAscending();

        foreach (var entry in entries)
        {
            var lowerComparison = _keyComparer.Compare(entry.Key, range.LowerBound);
            var upperComparison = _keyComparer.Compare(entry.Key, range.UpperBound);
            var insideLowerBound = lowerComparison > 0 ||
                                   range.IncludeLowerBound && lowerComparison == 0;
            var insideUpperBound = upperComparison < 0 ||
                                   range.IncludeUpperBound && upperComparison == 0;

            if (insideLowerBound && insideUpperBound)
                yield return entry;
        }
    }

    private IEnumerable<BTreeEntry<TKey, TValue>> ScanAscending()
    {
        var leaf = GetLeftmostLeaf();

        while (leaf != null)
        {
            foreach (var entry in leaf.MutableEntries)
                yield return entry;

            leaf = leaf.Next;
        }
    }

    private IEnumerable<BTreeEntry<TKey, TValue>> ScanDescending()
    {
        var leaf = GetRightmostLeaf();

        while (leaf != null)
        {
            for (var index = leaf.MutableEntries.Count - 1; index >= 0; index--)
                yield return leaf.MutableEntries[index];

            leaf = leaf.Previous;
        }
    }

    private BalancingTreeLeafNode<TKey, TValue> FindLeafForInsertion(TKey key)
        => FindLeafUsingFirstGreaterKey(key);

    private BalancingTreeLeafNode<TKey, TValue> FindFirstCandidateLeaf(TKey key)
    {
        BalancingTreeNode<TKey, TValue> node = BalancingTree.Root;

        // Equal separators route left because duplicate keys may span leaves.
        while (node is BalancingTreeInternalNode<TKey, TValue> internalNode)
        {
            var childIndex = FindFirstKeyAtLeast(internalNode.MutableSeparators, key);
            node = internalNode.MutableChildren[childIndex];
        }

        return (BalancingTreeLeafNode<TKey, TValue>)node;
    }

    private BalancingTreeLeafNode<TKey, TValue> FindLastCandidateLeaf(TKey key)
        => FindLeafUsingFirstGreaterKey(key);

    private BalancingTreeLeafNode<TKey, TValue> FindLeafUsingFirstGreaterKey(TKey key)
    {
        BalancingTreeNode<TKey, TValue> node = BalancingTree.Root;

        while (node is BalancingTreeInternalNode<TKey, TValue> internalNode)
        {
            var childIndex = FindFirstKeyGreaterThan(internalNode.MutableSeparators, key);
            node = internalNode.MutableChildren[childIndex];
        }

        return (BalancingTreeLeafNode<TKey, TValue>)node;
    }

    private int FindFirstEntryAtKey(
        IReadOnlyList<BTreeEntry<TKey, TValue>> entries,
        TKey key)
        => BinarySearch(entries.Count, index => _keyComparer.Compare(entries[index].Key, key) < 0);

    private int FindFirstEntryAfterKey(
        IReadOnlyList<BTreeEntry<TKey, TValue>> entries,
        TKey key)
        => BinarySearch(entries.Count, index => _keyComparer.Compare(entries[index].Key, key) <= 0);

    private int FindFirstKeyAtLeast(IReadOnlyList<TKey> keys, TKey key)
        => BinarySearch(keys.Count, index => _keyComparer.Compare(keys[index], key) < 0);

    private int FindFirstKeyGreaterThan(IReadOnlyList<TKey> keys, TKey key)
        => BinarySearch(keys.Count, index => _keyComparer.Compare(keys[index], key) <= 0);

    private static int BinarySearch(int count, Func<int, bool> moveRight)
    {
        var lower = 0;
        var upper = count;

        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;

            if (moveRight(middle))
                lower = middle + 1;
            else
                upper = middle;
        }

        return lower;
    }

    private bool KeysEqual(TKey left, TKey right)
        => _keyComparer.Compare(left, right) == 0;

    private void SplitLeaf(BalancingTreeLeafNode<TKey, TValue> leaf)
    {
        var splitIndex = (leaf.MutableEntries.Count + 1) / 2;
        var rightLeaf = new BalancingTreeLeafNode<TKey, TValue>
        {
            Parent = leaf.Parent,
            MutableEntries = leaf.MutableEntries.Skip(splitIndex).ToList(),
            Previous = leaf,
            Next = leaf.Next
        };

        if (leaf.Next != null)
            leaf.Next.Previous = rightLeaf;

        leaf.MutableEntries = leaf.MutableEntries.Take(splitIndex).ToList();
        leaf.Next = rightLeaf;
        InsertRightSibling(leaf, rightLeaf);
    }

    private void InsertRightSibling(
        BalancingTreeNode<TKey, TValue> node,
        BalancingTreeNode<TKey, TValue> rightNode)
    {
        if (node.Parent == null)
        {
            var root = new BalancingTreeInternalNode<TKey, TValue>
            {
                MutableChildren = [node, rightNode]
            };
            node.Parent = root;
            rightNode.Parent = root;
            BalancingTree.Root = root;
            RebuildSeparatorsFromChildren(root);
            return;
        }

        var parent = node.Parent;
        var nodeIndex = parent.MutableChildren.IndexOf(node);
        parent.MutableChildren.Insert(nodeIndex + 1, rightNode);
        rightNode.Parent = parent;
        RebuildSeparatorsFromChildren(parent);

        if (parent.MutableSeparators.Count > _maxEntries)
            SplitInternalNode(parent);
    }

    private void SplitInternalNode(BalancingTreeInternalNode<TKey, TValue> node)
    {
        var splitChildIndex = (node.MutableChildren.Count + 1) / 2;
        var rightNode = new BalancingTreeInternalNode<TKey, TValue>
        {
            Parent = node.Parent,
            MutableChildren = node.MutableChildren.Skip(splitChildIndex).ToList()
        };

        node.MutableChildren = node.MutableChildren.Take(splitChildIndex).ToList();

        foreach (var child in rightNode.MutableChildren)
            child.Parent = rightNode;

        RebuildSeparatorsFromChildren(node);
        RebuildSeparatorsFromChildren(rightNode);
        InsertRightSibling(node, rightNode);
    }

    private void RebalanceLeaf(BalancingTreeLeafNode<TKey, TValue> leaf)
    {
        if (leaf.Parent == null || leaf.MutableEntries.Count >= _minLeafEntries)
            return;

        var parent = leaf.Parent;
        var leafIndex = parent.MutableChildren.IndexOf(leaf);
        var left = leafIndex > 0
            ? (BalancingTreeLeafNode<TKey, TValue>)parent.MutableChildren[leafIndex - 1]
            : null;
        var right = leafIndex < parent.MutableChildren.Count - 1
            ? (BalancingTreeLeafNode<TKey, TValue>)parent.MutableChildren[leafIndex + 1]
            : null;

        if (left != null && left.MutableEntries.Count > _minLeafEntries)
        {
            leaf.MutableEntries.Insert(0, left.MutableEntries[^1]);
            left.MutableEntries.RemoveAt(left.MutableEntries.Count - 1);
            return;
        }

        if (right != null && right.MutableEntries.Count > _minLeafEntries)
        {
            leaf.MutableEntries.Add(right.MutableEntries[0]);
            right.MutableEntries.RemoveAt(0);
            return;
        }

        if (left != null)
            MergeLeaves(left, leaf, parent, leafIndex);
        else
            MergeLeaves(leaf, right!, parent, leafIndex + 1);

        RebalanceInternalNode(parent);
    }

    private static void MergeLeaves(
        BalancingTreeLeafNode<TKey, TValue> left,
        BalancingTreeLeafNode<TKey, TValue> right,
        BalancingTreeInternalNode<TKey, TValue> parent,
        int rightIndex)
    {
        left.MutableEntries.AddRange(right.MutableEntries);
        left.Next = right.Next;

        if (right.Next != null)
            right.Next.Previous = left;

        parent.MutableChildren.RemoveAt(rightIndex);
    }

    private void RebalanceInternalNode(BalancingTreeInternalNode<TKey, TValue> node)
    {
        RebuildSeparatorsFromChildren(node);

        if (node.Parent == null)
        {
            if (node.MutableChildren.Count == 1)
            {
                BalancingTree.Root = node.MutableChildren[0];
                BalancingTree.Root.Parent = null;
            }

            return;
        }

        if (node.MutableChildren.Count >= _minInternalChildren)
            return;

        var parent = node.Parent;
        var nodeIndex = parent.MutableChildren.IndexOf(node);
        var left = nodeIndex > 0
            ? (BalancingTreeInternalNode<TKey, TValue>)parent.MutableChildren[nodeIndex - 1]
            : null;
        var right = nodeIndex < parent.MutableChildren.Count - 1
            ? (BalancingTreeInternalNode<TKey, TValue>)parent.MutableChildren[nodeIndex + 1]
            : null;

        if (TryBorrowChildFromLeft(node, left) || TryBorrowChildFromRight(node, right))
            return;

        if (left != null)
            MergeInternalNodes(left, node, parent, nodeIndex);
        else
            MergeInternalNodes(node, right!, parent, nodeIndex + 1);

        RebalanceInternalNode(parent);
    }

    private bool TryBorrowChildFromLeft(
        BalancingTreeInternalNode<TKey, TValue> node,
        BalancingTreeInternalNode<TKey, TValue>? left)
    {
        if (left == null || left.MutableChildren.Count <= _minInternalChildren)
            return false;

        var child = left.MutableChildren[^1];
        left.MutableChildren.RemoveAt(left.MutableChildren.Count - 1);
        node.MutableChildren.Insert(0, child);
        child.Parent = node;
        RebuildSeparatorsFromChildren(left);
        RebuildSeparatorsFromChildren(node);
        return true;
    }

    private bool TryBorrowChildFromRight(
        BalancingTreeInternalNode<TKey, TValue> node,
        BalancingTreeInternalNode<TKey, TValue>? right)
    {
        if (right == null || right.MutableChildren.Count <= _minInternalChildren)
            return false;

        var child = right.MutableChildren[0];
        right.MutableChildren.RemoveAt(0);
        node.MutableChildren.Add(child);
        child.Parent = node;
        RebuildSeparatorsFromChildren(right);
        RebuildSeparatorsFromChildren(node);
        return true;
    }

    private static void MergeInternalNodes(
        BalancingTreeInternalNode<TKey, TValue> left,
        BalancingTreeInternalNode<TKey, TValue> right,
        BalancingTreeInternalNode<TKey, TValue> parent,
        int rightIndex)
    {
        foreach (var child in right.MutableChildren)
        {
            left.MutableChildren.Add(child);
            child.Parent = left;
        }

        parent.MutableChildren.RemoveAt(rightIndex);
        RebuildSeparatorsFromChildren(left);
    }

    private static void RefreshAncestors(BalancingTreeNode<TKey, TValue> node)
    {
        while (node.Parent != null)
        {
            var parent = node.Parent;
            RebuildSeparatorsFromChildren(parent);
            node = parent;
        }
    }

    private static void RebuildSeparatorsFromChildren(
        BalancingTreeInternalNode<TKey, TValue> node)
        => node.MutableSeparators =
            node.MutableChildren.Skip(1).Select(GetMinimumKey).ToList();

    private static TKey GetMinimumKey(BalancingTreeNode<TKey, TValue> node)
    {
        while (node is BalancingTreeInternalNode<TKey, TValue> internalNode)
            node = internalNode.MutableChildren[0];

        return ((BalancingTreeLeafNode<TKey, TValue>)node).MutableEntries[0].Key;
    }

    private BalancingTreeLeafNode<TKey, TValue> GetLeftmostLeaf()
    {
        var node = BalancingTree.Root;

        while (node is BalancingTreeInternalNode<TKey, TValue> internalNode)
            node = internalNode.MutableChildren[0];

        return (BalancingTreeLeafNode<TKey, TValue>)node;
    }

    private BalancingTreeLeafNode<TKey, TValue> GetRightmostLeaf()
    {
        var node = BalancingTree.Root;

        while (node is BalancingTreeInternalNode<TKey, TValue> internalNode)
            node = internalNode.MutableChildren[^1];

        return (BalancingTreeLeafNode<TKey, TValue>)node;
    }
}
