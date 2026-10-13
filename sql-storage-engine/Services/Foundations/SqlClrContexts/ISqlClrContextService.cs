// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlClrContexts;
internal interface ISqlClrContextService
{
    void RegisterSqlClrContext(SqlClrContext sqlClrContext);

    bool UnregisterSqlClrContext(SqlClrContext sqlClrContext);

    void RegisterSqlClrContextAssembly(SqlClrContext sqlClrContext);

    SqlClrContext ParseSqlClrContext(SqlClrContext sqlClrContext);

    string FormatSqlClrContext(SqlClrContext sqlClrContext);

    bool CheckSqlClrContext(SqlClrContext sqlClrContext);

    int CompareSqlClrContext(SqlClrContext sqlClrContext);

    SqlClrContext ReadSqlClrContextValidation(SqlClrContext sqlClrContext);
}