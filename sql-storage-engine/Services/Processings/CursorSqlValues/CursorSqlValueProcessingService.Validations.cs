// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CursorSqlValues;
internal sealed partial class CursorSqlValueProcessingService
{
    private static void ValidateCursor(CursorSqlValue cursorSqlValue)
    {
        ArgumentNullException.ThrowIfNull(argument: cursorSqlValue);
        ArgumentNullException.ThrowIfNull(argument: cursorSqlValue.Rows);

        if (cursorSqlValue.StorageFamily is not null || cursorSqlValue.IsNull || cursorSqlValue.Rows.Any(predicate: row => row is null))
        {
            throw new ArgumentException(message: "Invalid cursor SQL value.", paramName: nameof(cursorSqlValue));
        }
    }

    private static void ValidateFetch(CursorSqlValue cursorSqlValue, string direction, int position)
    {
        ValidateCursor(cursorSqlValue: cursorSqlValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: direction);

        if (!cursorSqlValue.IsOpen)
        {
            throw new InvalidOperationException(message: "The cursor is closed.");
        }
    }
}