// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Services.Managements.SqlMasks;

namespace sql_storage_engine.Exposures.Rows;
public sealed class SqlDataMaskingExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private SqlDataMaskingExposure()
    {
    }

    private static readonly ISqlMaskContextManagementService service = new SqlMaskContextManagementService();
    public static void Validate(SqlType sqlType, string function) =>
        service.CheckSqlMaskContext(sqlMaskContext: new SqlMaskContext { Type = sqlType, Function = function });

    public static SqlValue Apply(CatalogColumn catalogColumn, SqlValue sqlValue) =>
        service.ApplySqlMaskContext(sqlMaskContext: new SqlMaskContext { Column = catalogColumn, Value = sqlValue })
            .Result!;
}