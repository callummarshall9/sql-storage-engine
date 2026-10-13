// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.SqlExpressionEnvironments;
internal interface ISqlExpressionClockBroker
{
    DateTime ReadTimestamp(bool utc, object? timeProviderSource);

    Guid CreateIdentifier();
}