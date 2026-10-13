// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Processings.RowUpdates;

namespace sql_storage_engine.Exposures.Rows;
/// <summary>Creates immutable row-update snapshots through the row-update processing service.</summary>
public static class RowUpdateExposure
{
    private static readonly IRowUpdateProcessingService service = new RowUpdateProcessingService();
    public static RowUpdate CreateRowUpdate(IEnumerable<ColumnUpdate> columns) =>
        service.PrepareRowUpdate(rowUpdate: new RowUpdate { ColumnSource = columns });

    public static RowUpdate PrepareRowUpdate(RowUpdate rowUpdate) =>
        service.PrepareRowUpdate(rowUpdate: rowUpdate);
}