// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CursorSqlValues;
internal interface ICursorSqlValueProcessingService
{
    CursorSqlValue FetchCursorSqlValue(CursorSqlValue cursorSqlValue, string direction, int position);

    CursorSqlValue CloseCursorSqlValue(CursorSqlValue cursorSqlValue);
}