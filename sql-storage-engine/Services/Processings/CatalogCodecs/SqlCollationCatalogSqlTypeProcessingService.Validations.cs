// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal sealed partial class SqlCollationCatalogSqlTypeProcessingService
{
    private static void ValidateSqlTypeContext(SqlTypeContext sqlTypeContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlTypeContext);
    }

    private void ValidateCatalogScalarTypeCreation(SqlTypeContext sqlTypeContext)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlTypeContext);
        ArgumentNullException.ThrowIfNull(argument: sqlTypeContext.SqlTypeSource);
        ExecuteValidateDeclarationSqlType(sqlType: sqlTypeContext.SqlTypeSource);

        if (!ReadIsUserDefinedSqlType(sqlType: sqlTypeContext.SqlTypeSource))
        {
            throw new ArgumentException(message: "A scalar catalog type must be an alias or CLR user-defined type.", paramName: "definition");
        }
    }
}