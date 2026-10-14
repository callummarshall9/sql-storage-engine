// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.BalancingTrees;
public sealed class BalancingTreeLeafNode<TKey, TValue> : BalancingTreeNode<TKey, TValue>
{
    private IReadOnlyList<BTreeEntry<TKey, TValue>>? entries;
    public IReadOnlyList<BTreeEntry<TKey, TValue>> Entries { get => entries!; internal set => entries = value; }
    public BalancingTreeLeafNode<TKey, TValue>? Previous { get; internal set; }
    public BalancingTreeLeafNode<TKey, TValue>? Next { get; internal set; }

    private List<BTreeEntry<TKey, TValue>>? mutableEntries;
    internal List<BTreeEntry<TKey, TValue>> MutableEntries { get => mutableEntries!; set => mutableEntries = value; }
}