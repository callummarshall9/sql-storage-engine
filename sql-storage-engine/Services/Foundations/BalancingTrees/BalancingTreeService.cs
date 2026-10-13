// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models;
using sql_storage_engine.Models.BalancingTrees;

namespace sql_storage_engine.Services.Foundations.BalancingTrees;
internal sealed partial class BalancingTreeService<TKey, TValue> : IBalancingTreeService<TKey, TValue>
{
    private readonly IComparer<TKey> _keyComparer;
    private readonly IEqualityComparer<TValue> _valueComparer;
    private readonly int _maxEntries;
    private readonly int _minLeafEntries;
    private readonly int _minInternalChildren;
    private readonly BalancingTreeState<TKey, TValue> state;
    internal BalancingTreeService(BalancingTreeConfiguration<TKey, TValue> balancingTreeConfiguration)
    {
        ValidateConfiguration(balancingTreeConfiguration: balancingTreeConfiguration);
        _keyComparer = balancingTreeConfiguration.KeyComparer ?? Comparer<TKey>.Default;
        _valueComparer = balancingTreeConfiguration.ValueComparer ?? EqualityComparer<TValue>.Default;
        _maxEntries = balancingTreeConfiguration.Order - 1;
        _minLeafEntries = (_maxEntries + 1) / 2;
        _minInternalChildren = (balancingTreeConfiguration.Order + 1) / 2;
        state = new BalancingTreeState<TKey, TValue>
        {
            Tree = new BalancingTree<TKey, TValue>
            {
                Order = balancingTreeConfiguration.Order,
                Root = new BalancingTreeLeafNode<TKey, TValue>
                {
                    MutableEntries = []
                }
            }
        };
    }

    public BalancingTreeContext<TKey, TValue> ProcessBalancingTreeContext(BalancingTreeContext<TKey, TValue> balancingTreeContext) =>
        TryCatch(operation: () =>
    {
        ValidateRequest(balancingTreeContext: balancingTreeContext);

        switch (balancingTreeContext.Operation)
        {
            case 0:
                break;
            case 1:
                Add(key: balancingTreeContext.Key, value: balancingTreeContext.Value);
                break;
            case 2:
                balancingTreeContext.Found = Remove(key: balancingTreeContext.Key, value: balancingTreeContext.Value);
                break;
            case 3:
                balancingTreeContext.Found = ContainsKey(key: balancingTreeContext.Key);
                break;
            case 4:
                balancingTreeContext.Found = TryGetValue(key: balancingTreeContext.Key, value: out var result);
                balancingTreeContext.Value = result;
                break;
            case 5:
                balancingTreeContext.Values = Find(key: balancingTreeContext.Key);
                break;
            case 6:
                balancingTreeContext.Found = TryGetLowerBound(key: balancingTreeContext.Key, entry: out var lower);
                balancingTreeContext.Entry = lower;
                break;
            case 7:
                balancingTreeContext.Found = TryGetUpperBound(key: balancingTreeContext.Key, entry: out var upper);
                balancingTreeContext.Entry = upper;
                break;
            case 8:
                balancingTreeContext.Entries = Scan(direction: balancingTreeContext.Direction);
                break;
            case 9:
                balancingTreeContext.Entries = Scan(range: balancingTreeContext.Range!);
                break;
            case 10:
                RefreshNodeViews(balancingTreeNode: state.Tree!.Root);
                break;
        }

        balancingTreeContext.Order = state.Tree!.Order;
        balancingTreeContext.Count = state.Count;
        balancingTreeContext.Tree = state.Tree;
        return balancingTreeContext;
    });

    private static void RefreshNodeViews(BalancingTreeNode<TKey, TValue> balancingTreeNode)
    {
        if (balancingTreeNode is BalancingTreeLeafNode<TKey, TValue> leaf)
        {
            leaf.IsLeaf = true;

            leaf.Keys = leaf.MutableEntries.Select(selector: entry => entry.Key)
            .ToArray();

            leaf.Children = Array.Empty<BalancingTreeNode<TKey, TValue>>();
            leaf.Entries = leaf.MutableEntries.AsReadOnly();
        }
        else
        {
            var node = (BalancingTreeInternalNode<TKey, TValue>)balancingTreeNode;
            node.IsLeaf = false;
            node.Keys = node.MutableSeparators.AsReadOnly();
            node.Children = node.MutableChildren.AsReadOnly();

            foreach (var child in node.MutableChildren)
            {
                RefreshNodeViews(balancingTreeNode: child);
            }
        }
    }

    private void Add(TKey key, TValue value)
    {
        var leaf = FindLeafUsingFirstGreaterKey(key: key);
        var insertIndex = FindFirstEntryAfterKey(entries: leaf.MutableEntries, key: key);
        leaf.MutableEntries.Insert(index: insertIndex, item: new BTreeEntry<TKey, TValue> { Key = key, Value = value });
        state.Count++;

        if (leaf.MutableEntries.Count > _maxEntries)
        {
            SplitLeaf(leaf: leaf);
        }

        RefreshAncestors(node: leaf);
    }

    private bool Remove(TKey key, TValue value)
    {
        var leaf = FindFirstCandidateLeaf(key: key);

        while (leaf != null)
        {
            var entryIndex = FindFirstEntryAtKey(entries: leaf.MutableEntries, key: key);

            while (entryIndex < leaf.MutableEntries.Count && KeysEqual(left: leaf.MutableEntries[entryIndex].Key, right: key))
            {
                if (_valueComparer.Equals(x: leaf.MutableEntries[entryIndex].Value, y: value))
                {
                    leaf.MutableEntries.RemoveAt(index: entryIndex);
                    state.Count--;
                    RebalanceLeaf(leaf: leaf);

                    if (state.Count > 0)
                    {
                        RefreshAncestors(node: leaf);
                    }

                    return true;
                }

                entryIndex++;
            }

            if (leaf.MutableEntries.Count > 0 && _keyComparer.Compare(x: leaf.MutableEntries[0].Key, y: key) > 0)
            {
                break;
            }

            leaf = leaf.Next;
        }

        return false;
    }

    private bool ContainsKey(TKey key) =>
        TryGetValue(key: key, value: out _);

    private bool TryGetValue(TKey key, out TValue value)
    {
        foreach (var matchingValue in Find(key: key))
        {
            value = matchingValue;
            return true;
        }

        value = default!;
        return false;
    }

    private IEnumerable<TValue> Find(TKey key)
    {
        var leaf = FindFirstCandidateLeaf(key: key);

        while (leaf != null)
        {
            var entryIndex = FindFirstEntryAtKey(entries: leaf.MutableEntries, key: key);

            while (entryIndex < leaf.MutableEntries.Count && KeysEqual(left: leaf.MutableEntries[entryIndex].Key, right: key))
            {
                yield return leaf.MutableEntries[entryIndex].Value;
                entryIndex++;
            }

            if (leaf.MutableEntries.Count > 0 && _keyComparer.Compare(x: leaf.MutableEntries[^1].Key, y: key) > 0)
            {
                yield break;
            }

            leaf = leaf.Next;
        }
    }

    private bool TryGetLowerBound(TKey key, out BTreeEntry<TKey, TValue> entry)
    {
        var leaf = FindFirstCandidateLeaf(key: key);

        while (leaf != null)
        {
            var entryIndex = FindFirstEntryAtKey(entries: leaf.MutableEntries, key: key);

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

    private bool TryGetUpperBound(TKey key, out BTreeEntry<TKey, TValue> entry)
    {
        var leaf = FindLeafUsingFirstGreaterKey(key: key);

        while (leaf != null)
        {
            var entryIndex = FindFirstEntryAfterKey(entries: leaf.MutableEntries, key: key);

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

    private IEnumerable<BTreeEntry<TKey, TValue>> Scan(ScanDirection direction = ScanDirection.Ascending) =>
        direction == ScanDirection.Descending ? ScanDescending() : ScanAscending();

    private IEnumerable<BTreeEntry<TKey, TValue>> Scan(BTreeRange<TKey> range)
    {
        if (_keyComparer.Compare(x: range.LowerBound, y: range.UpperBound) > 0)
        {
            yield break;
        }

        var entries = range.Direction == ScanDirection.Descending ? ScanDescending() : ScanAscending();

        foreach (var entry in entries)
        {
            var lowerComparison = _keyComparer.Compare(x: entry.Key, y: range.LowerBound);
            var upperComparison = _keyComparer.Compare(x: entry.Key, y: range.UpperBound);
            var insideLowerBound = lowerComparison > 0 || range.IncludeLowerBound && lowerComparison == 0;
            var insideUpperBound = upperComparison < 0 || range.IncludeUpperBound && upperComparison == 0;

            if (insideLowerBound && insideUpperBound)
            {
                yield return entry;
            }
        }
    }

    private IEnumerable<BTreeEntry<TKey, TValue>> ScanAscending()
    {
        var leaf = GetLeftmostLeaf();

        while (leaf != null)
        {
            foreach (var entry in leaf.MutableEntries)
            {
                yield return entry;
            }

            leaf = leaf.Next;
        }
    }

    private IEnumerable<BTreeEntry<TKey, TValue>> ScanDescending()
    {
        var leaf = GetRightmostLeaf();

        while (leaf != null)
        {
            for (var index = leaf.MutableEntries.Count - 1; index >= 0; index--)
            {
                yield return leaf.MutableEntries[index];
            }

            leaf = leaf.Previous;
        }
    }

    private BalancingTreeLeafNode<TKey, TValue> FindFirstCandidateLeaf(TKey key)
    {
        BalancingTreeNode<TKey, TValue> node = state.Tree!.Root;

        while (node is BalancingTreeInternalNode<TKey, TValue> internalNode)
        {
            var childIndex = FindFirstKeyAtLeast(keys: internalNode.MutableSeparators, key: key);
            node = internalNode.MutableChildren[childIndex];
        }

        return (BalancingTreeLeafNode<TKey, TValue>)node;
    }

    private BalancingTreeLeafNode<TKey, TValue> FindLeafUsingFirstGreaterKey(TKey key)
    {
        BalancingTreeNode<TKey, TValue> node = state.Tree!.Root;

        while (node is BalancingTreeInternalNode<TKey, TValue> internalNode)
        {
            var childIndex = FindFirstKeyGreaterThan(keys: internalNode.MutableSeparators, key: key);
            node = internalNode.MutableChildren[childIndex];
        }

        return (BalancingTreeLeafNode<TKey, TValue>)node;
    }

    private int FindFirstEntryAtKey(IReadOnlyList<BTreeEntry<TKey, TValue>> entries, TKey key) =>
        BinarySearch(count: entries.Count, moveRight: index => _keyComparer.Compare(x: entries[index].Key, y: key) < 0);

    private int FindFirstEntryAfterKey(IReadOnlyList<BTreeEntry<TKey, TValue>> entries, TKey key) =>
        BinarySearch(count: entries.Count, moveRight: index => _keyComparer.Compare(x: entries[index].Key, y: key) <= 0);

    private int FindFirstKeyAtLeast(IReadOnlyList<TKey> keys, TKey key) =>
        BinarySearch(count: keys.Count, moveRight: index => _keyComparer.Compare(x: keys[index], y: key) < 0);

    private int FindFirstKeyGreaterThan(IReadOnlyList<TKey> keys, TKey key) =>
        BinarySearch(count: keys.Count, moveRight: index => _keyComparer.Compare(x: keys[index], y: key) <= 0);

    private static int BinarySearch(int count, Func<int, bool> moveRight)
    {
        var lower = 0;
        var upper = count;

        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;

            if (moveRight(arg: middle))
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle;
            }
        }

        return lower;
    }

    private bool KeysEqual(TKey left, TKey right) =>
        _keyComparer.Compare(x: left, y: right) == 0;

    private void SplitLeaf(BalancingTreeLeafNode<TKey, TValue> leaf)
    {
        var splitIndex = (leaf.MutableEntries.Count + 1) / 2;

        var rightLeaf = new BalancingTreeLeafNode<TKey, TValue>
        {
            Parent = leaf.Parent,
            MutableEntries = leaf.MutableEntries.Skip(count: splitIndex)
            .ToList(),
            Previous = leaf,
            Next = leaf.Next
        };

        if (leaf.Next != null)
        {
            leaf.Next.Previous = rightLeaf;
        }

        leaf.MutableEntries = leaf.MutableEntries.Take(count: splitIndex)
            .ToList();

        leaf.Next = rightLeaf;
        InsertRightSibling(node: leaf, rightNode: rightLeaf);
    }

    private void InsertRightSibling(BalancingTreeNode<TKey, TValue> node, BalancingTreeNode<TKey, TValue> rightNode)
    {
        if (node.Parent == null)
        {
            var root = new BalancingTreeInternalNode<TKey, TValue>
            {
                MutableSeparators = [],
                MutableChildren = [node, rightNode]
            };

            node.Parent = root;
            rightNode.Parent = root;
            state.Tree!.Root = root;
            RebuildSeparatorsFromChildren(node: root);
            return;
        }

        var parent = node.Parent;
        var nodeIndex = parent.MutableChildren.IndexOf(item: node);
        parent.MutableChildren.Insert(index: nodeIndex + 1, item: rightNode);
        rightNode.Parent = parent;
        RebuildSeparatorsFromChildren(node: parent);

        if (parent.MutableSeparators.Count > _maxEntries)
        {
            SplitInternalNode(node: parent);
        }
    }

    private void SplitInternalNode(BalancingTreeInternalNode<TKey, TValue> node)
    {
        var splitChildIndex = (node.MutableChildren.Count + 1) / 2;

        var rightNode = new BalancingTreeInternalNode<TKey, TValue>
        {
            Parent = node.Parent,
            MutableSeparators = [],
            MutableChildren = node.MutableChildren.Skip(count: splitChildIndex)
            .ToList()
        };

        node.MutableChildren = node.MutableChildren.Take(count: splitChildIndex)
            .ToList();

        foreach (var child in rightNode.MutableChildren)
        {
            child.Parent = rightNode;
        }

        RebuildSeparatorsFromChildren(node: node);
        RebuildSeparatorsFromChildren(node: rightNode);
        InsertRightSibling(node: node, rightNode: rightNode);
    }

    private void RebalanceLeaf(BalancingTreeLeafNode<TKey, TValue> leaf)
    {
        if (leaf.Parent == null || leaf.MutableEntries.Count >= _minLeafEntries)
        {
            return;
        }

        var parent = leaf.Parent;
        var leafIndex = parent.MutableChildren.IndexOf(item: leaf);
        var left = leafIndex > 0 ? (BalancingTreeLeafNode<TKey, TValue>)parent.MutableChildren[leafIndex - 1] : null;
        var right = leafIndex < parent.MutableChildren.Count - 1 ? (BalancingTreeLeafNode<TKey, TValue>)parent.MutableChildren[leafIndex + 1] : null;

        if (left != null && left.MutableEntries.Count > _minLeafEntries)
        {
            leaf.MutableEntries.Insert(index: 0, item: left.MutableEntries[^1]);
            left.MutableEntries.RemoveAt(index: left.MutableEntries.Count - 1);
            return;
        }

        if (right != null && right.MutableEntries.Count > _minLeafEntries)
        {
            leaf.MutableEntries.Add(item: right.MutableEntries[0]);
            right.MutableEntries.RemoveAt(index: 0);
            return;
        }

        if (left != null)
        {
            MergeLeaves(left: left, right: leaf, parent: parent, rightIndex: leafIndex);
        }
        else
        {
            MergeLeaves(left: leaf, right: right!, parent: parent, rightIndex: leafIndex + 1);
        }

        RebalanceInternalNode(node: parent);
    }

    private static void MergeLeaves(BalancingTreeLeafNode<TKey, TValue> left, BalancingTreeLeafNode<TKey, TValue> right, BalancingTreeInternalNode<TKey, TValue> parent, int rightIndex)
    {
        left.MutableEntries.AddRange(collection: right.MutableEntries);
        left.Next = right.Next;

        if (right.Next != null)
        {
            right.Next.Previous = left;
        }

        parent.MutableChildren.RemoveAt(index: rightIndex);
    }

    private void RebalanceInternalNode(BalancingTreeInternalNode<TKey, TValue> node)
    {
        RebuildSeparatorsFromChildren(node: node);

        if (node.Parent == null)
        {
            if (node.MutableChildren.Count == 1)
            {
                state.Tree!.Root = node.MutableChildren[0];
                state.Tree!.Root.Parent = null;
            }

            return;
        }

        if (node.MutableChildren.Count >= _minInternalChildren)
        {
            return;
        }

        var parent = node.Parent;
        var nodeIndex = parent.MutableChildren.IndexOf(item: node);
        var left = nodeIndex > 0 ? (BalancingTreeInternalNode<TKey, TValue>)parent.MutableChildren[nodeIndex - 1] : null;
        var right = nodeIndex < parent.MutableChildren.Count - 1 ? (BalancingTreeInternalNode<TKey, TValue>)parent.MutableChildren[nodeIndex + 1] : null;

        if (TryBorrowChildFromLeft(node: node, left: left) || TryBorrowChildFromRight(node: node, right: right))
        {
            return;
        }

        if (left != null)
        {
            MergeInternalNodes(left: left, right: node, parent: parent, rightIndex: nodeIndex);
        }
        else
        {
            MergeInternalNodes(left: node, right: right!, parent: parent, rightIndex: nodeIndex + 1);
        }

        RebalanceInternalNode(node: parent);
    }

    private bool TryBorrowChildFromLeft(BalancingTreeInternalNode<TKey, TValue> node, BalancingTreeInternalNode<TKey, TValue>? left)
    {
        if (left == null || left.MutableChildren.Count <= _minInternalChildren)
        {
            return false;
        }

        var child = left.MutableChildren[^1];
        left.MutableChildren.RemoveAt(index: left.MutableChildren.Count - 1);
        node.MutableChildren.Insert(index: 0, item: child);
        child.Parent = node;
        RebuildSeparatorsFromChildren(node: left);
        RebuildSeparatorsFromChildren(node: node);
        return true;
    }

    private bool TryBorrowChildFromRight(BalancingTreeInternalNode<TKey, TValue> node, BalancingTreeInternalNode<TKey, TValue>? right)
    {
        if (right == null || right.MutableChildren.Count <= _minInternalChildren)
        {
            return false;
        }

        var child = right.MutableChildren[0];
        right.MutableChildren.RemoveAt(index: 0);
        node.MutableChildren.Add(item: child);
        child.Parent = node;
        RebuildSeparatorsFromChildren(node: right);
        RebuildSeparatorsFromChildren(node: node);
        return true;
    }

    private static void MergeInternalNodes(BalancingTreeInternalNode<TKey, TValue> left, BalancingTreeInternalNode<TKey, TValue> right, BalancingTreeInternalNode<TKey, TValue> parent, int rightIndex)
    {
        foreach (var child in right.MutableChildren)
        {
            left.MutableChildren.Add(item: child);
            child.Parent = left;
        }

        parent.MutableChildren.RemoveAt(index: rightIndex);
        RebuildSeparatorsFromChildren(node: left);
    }

    private static void RefreshAncestors(BalancingTreeNode<TKey, TValue> node)
    {
        while (node.Parent != null)
        {
            var parent = node.Parent;
            RebuildSeparatorsFromChildren(node: parent);
            node = parent;
        }
    }

    private static void RebuildSeparatorsFromChildren(BalancingTreeInternalNode<TKey, TValue> node) =>
        node.MutableSeparators = node.MutableChildren.Skip(count: 1)
            .Select(selector: GetMinimumKey)
            .ToList();

    private static TKey GetMinimumKey(BalancingTreeNode<TKey, TValue> node)
    {
        while (node is BalancingTreeInternalNode<TKey, TValue> internalNode)
        {
            node = internalNode.MutableChildren[0];
        }

        return ((BalancingTreeLeafNode<TKey, TValue>)node).MutableEntries[0].Key;
    }

    private BalancingTreeLeafNode<TKey, TValue> GetLeftmostLeaf()
    {
        var node = state.Tree!.Root;

        while (node is BalancingTreeInternalNode<TKey, TValue> internalNode)
        {
            node = internalNode.MutableChildren[0];
        }

        return (BalancingTreeLeafNode<TKey, TValue>)node;
    }

    private BalancingTreeLeafNode<TKey, TValue> GetRightmostLeaf()
    {
        var node = state.Tree!.Root;

        while (node is BalancingTreeInternalNode<TKey, TValue> internalNode)
        {
            node = internalNode.MutableChildren[^1];
        }

        return (BalancingTreeLeafNode<TKey, TValue>)node;
    }
}