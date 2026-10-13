// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.SqlGuidOrders;
internal interface ISqlGuidOrderBroker
{
    int Compare(Guid left, Guid right);
}