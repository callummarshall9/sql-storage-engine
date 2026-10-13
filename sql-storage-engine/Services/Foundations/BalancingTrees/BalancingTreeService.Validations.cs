// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.BalancingTrees;

namespace sql_storage_engine.Services.Foundations.BalancingTrees;
internal sealed partial class BalancingTreeService<TKey, TValue>
{
    private static void ValidateConfiguration(BalancingTreeConfiguration<TKey, TValue> balancingTreeConfiguration)
    {
        ArgumentNullException.ThrowIfNull(argument: balancingTreeConfiguration);

        if (balancingTreeConfiguration.Order < 3)
        {
            throw new ArgumentOutOfRangeException(paramName: "order", actualValue: balancingTreeConfiguration.Order, message: "Order must be at least three.");
        }
    }

    private static void ValidateRequest(BalancingTreeContext<TKey, TValue> balancingTreeContext)
    {
        ArgumentNullException.ThrowIfNull(argument: balancingTreeContext);

        if (balancingTreeContext.Operation is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(balancingTreeContext));
        }

        if (balancingTreeContext.Operation == 9)
        {
            ArgumentNullException.ThrowIfNull(argument: balancingTreeContext.Range);
        }
    }
}