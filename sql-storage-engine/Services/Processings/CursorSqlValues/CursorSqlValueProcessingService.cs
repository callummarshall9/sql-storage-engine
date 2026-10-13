// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CursorSqlValues;
internal sealed partial class CursorSqlValueProcessingService : ICursorSqlValueProcessingService
{
    public CursorSqlValue FetchCursorSqlValue(CursorSqlValue cursorSqlValue, string direction, int position) =>
        TryCatch(operation: () =>
    {
        ValidateFetch(cursorSqlValue: cursorSqlValue, direction: direction, position: position);

        var next = direction switch
        {
            "Next" => checked(cursorSqlValue.Position + 1),
            "Prior" => checked(cursorSqlValue.Position - 1),
            "First" => 0,
            "Last" => cursorSqlValue.Rows.Count - 1,
            "Absolute" => position > 0 ? position - 1 : checked(cursorSqlValue.Rows.Count + position),
            "Relative" => checked(cursorSqlValue.Position + position),
            _ => throw new ArgumentException(message: "Unknown cursor direction.", paramName: nameof(direction))};

        return cursorSqlValue with
        {
            Position = next,
            CurrentRow = next >= 0 && next < cursorSqlValue.Rows.Count ? cursorSqlValue.Rows[next] : null
        };
    });

    public CursorSqlValue CloseCursorSqlValue(CursorSqlValue cursorSqlValue) =>
        TryCatch(operation: () =>
    {
        ValidateCursor(cursorSqlValue: cursorSqlValue);

        return cursorSqlValue with
        {
            IsOpen = false,
            Position = -1,
            CurrentRow = null
        };
    });
}