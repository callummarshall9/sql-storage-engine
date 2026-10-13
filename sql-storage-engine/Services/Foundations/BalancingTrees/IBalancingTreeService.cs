// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.BalancingTrees;

namespace sql_storage_engine.Services.Foundations.BalancingTrees;
internal interface IBalancingTreeService<TKey, TValue>
{
    BalancingTreeContext<TKey, TValue> ProcessBalancingTreeContext(BalancingTreeContext<TKey, TValue> balancingTreeContext);
}