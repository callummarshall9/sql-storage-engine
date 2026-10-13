// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlTypeValues;
internal sealed partial class SqlTypeValueContextOrchestrationService
{
    private static void ValidateSqlTypeContext(SqlTypeContext sqlTypeContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlTypeContext);
        ArgumentNullException.ThrowIfNull(argument: sqlTypeContext.SqlTypeSource);
        ArgumentNullException.ThrowIfNull(argument: sqlTypeContext.InputSqlValueValue);
    }

    private static void ValidateRequest(SqlTypeContext sqlTypeContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlTypeContext);
    }
}