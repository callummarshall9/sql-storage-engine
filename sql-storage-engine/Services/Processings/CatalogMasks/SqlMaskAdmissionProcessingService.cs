// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Processings.CatalogMasks;
internal sealed partial class SqlMaskAdmissionProcessingService : ISqlMaskAdmissionProcessingService
{
    private readonly sql_storage_engine.Services.Foundations.SqlMaskAdmissions.SqlMaskAdmissionService sqlMaskAdmissionService = new sql_storage_engine.Services.Foundations.SqlMaskAdmissions.SqlMaskAdmissionService();
    public SqlMaskContext CheckSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidateSqlMaskContext(sqlMaskContext: sqlMaskContext);
        return sqlMaskAdmissionService.CheckSqlMaskContext(sqlMaskContext: sqlMaskContext with { Family = ReadMaskFamily(type: sqlMaskContext.Type!) });
    });

    private static SqlStorageFamily ReadMaskFamily(SqlType type)
    {
        if (type.BaseType is not null)
        {
            return ReadMaskFamily(type: type.BaseType);
        }

        return type.Name switch
        {
            SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.Text or SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText => SqlStorageFamily.Text,
            SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or SqlTypeName.BigInt => SqlStorageFamily.Integer,
            SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.Money or SqlTypeName.SmallMoney => SqlStorageFamily.Decimal,
            SqlTypeName.Float or SqlTypeName.Real => SqlStorageFamily.Float,
            SqlTypeName.Date => SqlStorageFamily.Date,
            SqlTypeName.Time => SqlStorageFamily.Time,
            SqlTypeName.DateTime or SqlTypeName.DateTime2 or SqlTypeName.SmallDateTime => SqlStorageFamily.DateTime,
            SqlTypeName.DateTimeOffset => SqlStorageFamily.DateTimeOffset,
            _ => SqlStorageFamily.Binary
        };
    }
}