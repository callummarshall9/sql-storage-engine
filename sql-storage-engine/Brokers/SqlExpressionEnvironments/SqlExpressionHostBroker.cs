// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Dependencies.SqlExpressionEnvironments;

namespace sql_storage_engine.Brokers.SqlExpressionEnvironments;
internal sealed class SqlExpressionHostBroker : ISqlExpressionHostBroker
{
    private readonly SqlExpressionHostDependency dependency;
    public SqlExpressionHostBroker(SqlExpressionHostDependency dependency)
    {
        this.dependency = dependency;
    }

    public void Register(Func<string, object, object>? runtime) =>
        dependency.Register(runtime: runtime);

    public object? Evaluate(string source, object columns) =>
        dependency.Evaluate(source: source, columns: columns);

    internal static SqlExpressionHostBroker CreateSqlExpressionHostBroker() =>
        new(dependency: new sql_storage_engine.Dependencies.SqlExpressionEnvironments.SqlExpressionHostDependency());
}