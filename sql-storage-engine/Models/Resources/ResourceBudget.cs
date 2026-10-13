// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Models.Resources;
internal sealed class ResourceBudget
{
    public StorageLimits Limits { get => limits!; init => limits = value; }
    public CapacityCounter Transactions { get => transactions!; init => transactions = value; }
    public CapacityCounter Pins { get => pins!; init => pins = value; }

    private StorageLimits? limits;
    private CapacityCounter? transactions;
    private CapacityCounter? pins;
}