// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.Rows;
internal interface IRowProcessingService
{
    Row PrepareRow(Row row);
}