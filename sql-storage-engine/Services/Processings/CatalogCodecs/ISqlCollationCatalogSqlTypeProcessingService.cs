// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogCodecs;
internal interface ISqlCollationCatalogSqlTypeProcessingService
{
    SqlTypeContext ProcessSqlTypeContextRestore73(SqlTypeContext sqlTypeContext);

    SqlTypeContext CreateSqlTypeContextCatalogScalarType(SqlTypeContext sqlTypeContext);
}