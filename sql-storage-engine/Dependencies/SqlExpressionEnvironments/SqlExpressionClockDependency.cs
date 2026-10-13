// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Dependencies.SqlExpressionEnvironments;
internal sealed class SqlExpressionClockDependency : TimeProvider
{
    public override DateTimeOffset GetUtcNow() =>
        TimeProvider.System.GetUtcNow();
    public override TimeZoneInfo LocalTimeZone => TimeProvider.System.LocalTimeZone;

    public DateTime ReadTimestamp(bool utc, object? timeProviderSource) =>
        DateTime.SpecifyKind(value: utc ? (timeProviderSource as TimeProvider ?? this).GetUtcNow()
            .UtcDateTime : (timeProviderSource as TimeProvider ?? this).GetLocalNow()
            .DateTime, kind: DateTimeKind.Unspecified);

    public Guid CreateIdentifier() =>
        Guid.NewGuid();
}