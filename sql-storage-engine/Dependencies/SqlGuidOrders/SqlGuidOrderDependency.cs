// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;

namespace sql_storage_engine.Dependencies.SqlGuidOrders;
internal sealed class SqlGuidOrderDependency : Comparer<Guid>
{
    private readonly Comparer<SqlGuid> comparer = Comparer<SqlGuid>.Default;
    public override int Compare(Guid x, Guid y) =>
        comparer.Compare(x: new SqlGuid(g: x), y: new SqlGuid(g: y));
}