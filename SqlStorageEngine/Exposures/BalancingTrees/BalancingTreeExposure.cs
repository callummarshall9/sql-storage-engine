// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models;
using sql_storage_engine.Models.BalancingTrees;
using sql_storage_engine.Services.Foundations.BalancingTrees;

namespace sql_storage_engine.Exposures.BalancingTrees;
public sealed class BalancingTreeExposure<TKey, TValue> : IBalancingTreeExposure<TKey, TValue>
{
    private readonly BalancingTreeService<TKey, TValue> service;
    public BalancingTreeExposure(BalancingTreeConfiguration<TKey, TValue> balancingTreeConfiguration)
    {
        service = new BalancingTreeService<TKey, TValue>(balancingTreeConfiguration: balancingTreeConfiguration);
    }

    public int ReadOrder() =>
        service.ProcessBalancingTreeContext(balancingTreeContext: new BalancingTreeContext<TKey, TValue> { Operation = 0 })
            .Order;

    public int ReadCount() =>
        service.ProcessBalancingTreeContext(balancingTreeContext: new BalancingTreeContext<TKey, TValue> { Operation = 0 })
            .Count;

    public BalancingTree<TKey, TValue> ReadBalancingTree() =>
        service.ProcessBalancingTreeContext(balancingTreeContext: new BalancingTreeContext<TKey, TValue> { Operation = 10 })
            .Tree!;

    public void Add(TKey key, TValue value) =>
        service.ProcessBalancingTreeContext(balancingTreeContext: new BalancingTreeContext<TKey, TValue> { Operation = 1, Key = key, Value = value });

    public bool Remove(TKey key, TValue value) =>
        service.ProcessBalancingTreeContext(balancingTreeContext: new BalancingTreeContext<TKey, TValue> { Operation = 2, Key = key, Value = value })
            .Found;

    public bool ContainsKey(TKey key) =>
        service.ProcessBalancingTreeContext(balancingTreeContext: new BalancingTreeContext<TKey, TValue> { Operation = 3, Key = key })
            .Found;

    public bool TryGetValue(TKey key, out TValue value)
    {
        var result = service.ProcessBalancingTreeContext(balancingTreeContext: new BalancingTreeContext<TKey, TValue> { Operation = 4, Key = key });
        value = result.Value;
        return result.Found;
    }

    public IEnumerable<TValue> Find(TKey key) =>
        service.ProcessBalancingTreeContext(balancingTreeContext: new BalancingTreeContext<TKey, TValue> { Operation = 5, Key = key })
            .Values!;

    public bool TryGetLowerBound(TKey key, out BTreeEntry<TKey, TValue> entry)
    {
        var result = service.ProcessBalancingTreeContext(balancingTreeContext: new BalancingTreeContext<TKey, TValue> { Operation = 6, Key = key });
        entry = result.Entry;
        return result.Found;
    }

    public bool TryGetUpperBound(TKey key, out BTreeEntry<TKey, TValue> entry)
    {
        var result = service.ProcessBalancingTreeContext(balancingTreeContext: new BalancingTreeContext<TKey, TValue> { Operation = 7, Key = key });
        entry = result.Entry;
        return result.Found;
    }

    public IEnumerable<BTreeEntry<TKey, TValue>> Scan(ScanDirection direction = ScanDirection.Ascending) =>
        service.ProcessBalancingTreeContext(balancingTreeContext: new BalancingTreeContext<TKey, TValue> { Operation = 8, Direction = direction })
            .Entries!;

    public IEnumerable<BTreeEntry<TKey, TValue>> Scan(BTreeRange<TKey> bTreeRange) =>
        service.ProcessBalancingTreeContext(balancingTreeContext: new BalancingTreeContext<TKey, TValue> { Operation = 9, Range = bTreeRange })
            .Entries!;
}