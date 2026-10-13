// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Processings.CursorSqlValues;

namespace sql_storage_engine.Exposures.Rows;
public static class CursorSqlValueExposure
{
    private static readonly ICursorSqlValueProcessingService service = new CursorSqlValueProcessingService();
    public static CursorSqlValue FetchCursorSqlValue(CursorSqlValue cursorSqlValue, string direction, int position, out Row? row)
    {
        var result = service.FetchCursorSqlValue(cursorSqlValue: cursorSqlValue, direction: direction, position: position);
        row = result.CurrentRow;
        return result;
    }

    public static CursorSqlValue CloseCursorSqlValue(CursorSqlValue cursorSqlValue) =>
        service.CloseCursorSqlValue(cursorSqlValue: cursorSqlValue);
}