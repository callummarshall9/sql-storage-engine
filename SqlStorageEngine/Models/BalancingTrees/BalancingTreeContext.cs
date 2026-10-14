// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.BalancingTrees;
public sealed class BalancingTreeContext<TKey, TValue>
{
    public int Operation { get; set; }

    private TKey? key;
    public TKey Key { get => key!; set => key = value; }

    private TValue? inputValue;
    public TValue Value { get => inputValue!; set => inputValue = value; }
    public bool Found { get; set; }
    public int Order { get; set; }
    public int Count { get; set; }
    public BalancingTree<TKey, TValue>? Tree { get; set; }
    public BTreeEntry<TKey, TValue> Entry { get; set; }
    public ScanDirection Direction { get; set; }
    public BTreeRange<TKey>? Range { get; set; }
    public IEnumerable<TValue>? Values { get; set; }
    public IEnumerable<BTreeEntry<TKey, TValue>>? Entries { get; set; }
}