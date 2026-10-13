// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Dependencies.SqlGuidOrders;

namespace sql_storage_engine.Brokers.SqlGuidOrders;
internal sealed class SqlGuidOrderBroker : ISqlGuidOrderBroker
{
    private readonly SqlGuidOrderDependency dependency;
    public SqlGuidOrderBroker(SqlGuidOrderDependency dependency)
    {
        this.dependency = dependency;
    }

    public int Compare(Guid left, Guid right) =>
        dependency.Compare(x: left, y: right);

    internal static SqlGuidOrderBroker CreateSqlGuidOrderBroker() =>
        new(dependency: new sql_storage_engine.Dependencies.SqlGuidOrders.SqlGuidOrderDependency());
}