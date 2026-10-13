// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Processings.Rows;

namespace sql_storage_engine.Exposures.Rows;
/// <summary>Prepares validated immutable row snapshots through the row processing contract.</summary>
public static class RowExposure
{
    private static readonly IRowProcessingService service = new RowProcessingService();
    public static Row CreateRow(IEnumerable<SqlValue> values) =>
        service.PrepareRow(row: new Row { ValueSource = values });

    public static Row PrepareRow(Row row) =>
        service.PrepareRow(row: row);
}