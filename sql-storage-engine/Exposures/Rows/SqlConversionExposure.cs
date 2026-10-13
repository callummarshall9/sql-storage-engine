// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Exposures.Rows;
public sealed class SqlConversionExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private SqlConversionExposure()
    {
    }

    private static readonly sql_storage_engine.Services.Coordinations.SqlTypes.ISqlTypeContextCoordinationService service = sql_storage_engine.Services.Coordinations.SqlTypes.SqlTypeContextCoordinationService.CreateSqlTypeContextCoordinationService();
    public static SqlValue ConvertTo(SqlType sqlType, SqlValue sqlValue) =>
        service.ProcessSqlTypeContextConvert78(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlType, InputSqlValueValue = sqlValue })
            .SqlValueResult!;

    public static SqlValue ConvertTo(SqlType[] sqlTypes, SqlValue sqlValue) =>
        service.ProcessSqlTypeContextConvert79(sqlTypeContext: new SqlTypeContext { SqlTypes = sqlTypes, InputSqlValueValue = sqlValue })
            .SqlValueResult!;
}