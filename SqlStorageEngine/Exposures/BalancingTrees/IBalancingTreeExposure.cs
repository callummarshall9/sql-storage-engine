// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models;

namespace sql_storage_engine.Exposures.BalancingTrees;
public interface IBalancingTreeExposure<TKey, TValue>
{
    int ReadOrder();

    int ReadCount();

    void Add(TKey key, TValue value);

    bool Remove(TKey key, TValue value);

    bool ContainsKey(TKey key);

    bool TryGetValue(TKey key, out TValue value);

    IEnumerable<TValue> Find(TKey key);

    bool TryGetLowerBound(TKey key, out BTreeEntry<TKey, TValue> entry);

    bool TryGetUpperBound(TKey key, out BTreeEntry<TKey, TValue> entry);

    IEnumerable<BTreeEntry<TKey, TValue>> Scan(ScanDirection direction = ScanDirection.Ascending);

    IEnumerable<BTreeEntry<TKey, TValue>> Scan(BTreeRange<TKey> bTreeRange);
}