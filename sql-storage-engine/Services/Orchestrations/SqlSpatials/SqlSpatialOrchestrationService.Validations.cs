// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlSpatials;
internal sealed partial class SqlSpatialOrchestrationService
{
    private static void ValidateCreation(SqlSpatial sqlSpatial)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlSpatial);

        if (!Enum.IsDefined(value: sqlSpatial.Kind))
        {
            throw new ArgumentOutOfRangeException(paramName: "kind");
        }

        if (sqlSpatial.Shape is null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(argument: sqlSpatial.WellKnownText);
        }
    }

    private static void ValidateSqlSpatial(SqlSpatial sqlSpatial)
    {
        ValidateCreation(sqlSpatial: sqlSpatial);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: sqlSpatial.WellKnownText);
    }

    private static void ValidateSqlSpatials(SqlSpatial[] sqlSpatials)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlSpatials);

        if (sqlSpatials.Length != 2)
        {
            throw new ArgumentException(message: "Two spatial values are required.");
        }

        foreach (var value in sqlSpatials)
        {
            ValidateSqlSpatial(sqlSpatial: value);
        }
    }

    private static void ValidatePoint(SqlSpatial sqlSpatial, int position)
    {
        ValidateSqlSpatial(sqlSpatial: sqlSpatial);

        if (position < 1)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(position));
        }
    }
}