// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Managements.SqlMasks;
internal sealed partial class SqlMaskContextManagementService : ISqlMaskContextManagementService
{
    private readonly sql_storage_engine.Services.Coordinations.SqlTypes.SqlTypeContextCoordinationService types = sql_storage_engine.Services.Coordinations.SqlTypes.SqlTypeContextCoordinationService.CreateSqlTypeContextCoordinationService();
    private readonly sql_storage_engine.Services.Coordinations.SqlMasks.SqlMaskContextCoordinationService masks = new sql_storage_engine.Services.Coordinations.SqlMasks.SqlMaskContextCoordinationService();
    public SqlMaskContext CheckSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidateCheckSqlMaskContext(sqlMaskContext: sqlMaskContext);

        var family = types.ProcessSqlTypeContextStorageFamily3(sqlTypeContext: new SqlTypeContext { SqlTypeSource = sqlMaskContext.Type })
            .StorageFamilyResult;

        return masks.CheckSqlMaskContext(sqlMaskContext: sqlMaskContext with { Family = family });
    });

    public SqlMaskContext ApplySqlMaskContext(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidateApplySqlMaskContext(sqlMaskContext: sqlMaskContext);

        if (sqlMaskContext.Column!.MaskingFunction is null || sqlMaskContext.Value!.IsNull)
        {
            return sqlMaskContext with
            {
                Result = sqlMaskContext.Value
            };
        }

        var type = sqlMaskContext.Column.Type;

        var family = types.ProcessSqlTypeContextStorageFamily3(sqlTypeContext: new SqlTypeContext { SqlTypeSource = type })
            .StorageFamilyResult;

        var context = masks.ReadSqlMaskContext(sqlMaskContext: sqlMaskContext with { Type = type, Family = family, Function = sqlMaskContext.Column.MaskingFunction });

        if (context.Kind is "email" or "partial")
        {
            var textType = types.ProcessSqlTypeContextNVarCharMax63(sqlTypeContext: new SqlTypeContext())
            .SqlTypeResult;

            var textValue = types.ProcessSqlTypeContextConvert78(sqlTypeContext: new SqlTypeContext { SqlTypeSource = textType, InputSqlValueValue = sqlMaskContext.Value })
            .SqlValueResult!;

            context = context with
            {
                Text = ((TextSqlValue)textValue).Value
            };
        }

        var applied = masks.ApplySqlMaskContext(sqlMaskContext: context);

        if (context.Kind == "random")
        {
            var converted = types.ProcessSqlTypeContextConvert78(sqlTypeContext: new SqlTypeContext { SqlTypeSource = type, InputSqlValueValue = applied.Result })
            .SqlValueResult;

            return applied with
            {
                Result = converted
            };
        }

        return applied;
    });
}