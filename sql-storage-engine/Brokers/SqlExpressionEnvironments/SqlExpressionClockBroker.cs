// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Dependencies.SqlExpressionEnvironments;

namespace sql_storage_engine.Brokers.SqlExpressionEnvironments;
internal sealed class SqlExpressionClockBroker : ISqlExpressionClockBroker
{
    private readonly SqlExpressionClockDependency dependency;
    public SqlExpressionClockBroker(SqlExpressionClockDependency dependency)
    {
        this.dependency = dependency;
    }

    public DateTime ReadTimestamp(bool utc, object? timeProviderSource) =>
        dependency.ReadTimestamp(utc: utc, timeProviderSource: timeProviderSource);

    public Guid CreateIdentifier() =>
        dependency.CreateIdentifier();

    internal static SqlExpressionClockBroker CreateSqlExpressionClockBroker() =>
        new(dependency: new sql_storage_engine.Dependencies.SqlExpressionEnvironments.SqlExpressionClockDependency());
}