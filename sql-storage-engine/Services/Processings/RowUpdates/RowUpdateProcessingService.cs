// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.RowUpdates;
internal sealed partial class RowUpdateProcessingService : IRowUpdateProcessingService
{
    public RowUpdate PrepareRowUpdate(RowUpdate rowUpdate) =>
        TryCatch(operation: () =>
    {
        ValidateRowUpdate(rowUpdate: rowUpdate);

        return new RowUpdate
        {
            Columns = Array.AsReadOnly(array: (rowUpdate.ColumnSource ?? rowUpdate.Columns).ToArray())
        };
    });
}