// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.SqlTypes;
internal sealed partial class SqlTypeContextCoordinationService
{
    private static void ValidateSqlTypeContext(SqlTypeContext sqlTypeContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlTypeContext);
    }
}