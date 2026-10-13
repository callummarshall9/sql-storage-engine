// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Foundations.SqlMaskAdmissions;
internal sealed partial class SqlMaskAdmissionService : ISqlMaskAdmissionService
{
    public SqlMaskContext CheckSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidateCheckSqlMaskContext(sqlMaskContext: sqlMaskContext);

        switch (sqlMaskContext.Kind)
        {
            case "default":
                break;
            case "email":
            case "partial":
                if (sqlMaskContext.Family != SqlStorageFamily.Text)
                {
                    throw new ArgumentException(message: "Email and partial masks require a character column.", paramName: "function");
                }

                break;
            case "random":
                if (sqlMaskContext.Family is not (SqlStorageFamily.Integer or SqlStorageFamily.Decimal or SqlStorageFamily.Float))
                {
                    throw new ArgumentException(message: "A random mask requires a numeric column.", paramName: "function");
                }

                if (sqlMaskContext.Start > sqlMaskContext.End)
                {
                    throw new ArgumentException(message: "A random mask requires an ascending range.", paramName: "function");
                }

                break;
            case "datetime":
                if (sqlMaskContext.Family is not (SqlStorageFamily.Date or SqlStorageFamily.Time or SqlStorageFamily.DateTime or SqlStorageFamily.DateTimeOffset))
                {
                    throw new ArgumentException(message: "A datetime mask requires a date or time column.", paramName: "function");
                }

                break;
            default:
                throw new ArgumentException(message: $"Unsupported masking function '{sqlMaskContext.Function}'.", paramName: "function");
        }

        return sqlMaskContext;
    });
}