// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.SqlExpressionEnvironments;
internal interface ISqlExpressionHostBroker
{
    void Register(Func<string, object, object>? runtime);

    object? Evaluate(string source, object columns);
}