// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.SqlTypeValues;
internal interface ISqlTypeValueContextOrchestrationService
{
    SqlTypeContext CheckSqlTypeContext(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextHierarchyParse(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextHierarchyRead(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextHierarchyFormat(SqlTypeContext sqlTypeContext);

    SqlTypeContext ProcessSqlTypeContextSpatialRead(SqlTypeContext sqlTypeContext);
}