// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.Rows;
internal sealed partial class RowProcessingService : IRowProcessingService
{
    public Row PrepareRow(Row row) =>
        TryCatch(operation: () =>
    {
        ValidateRow(row: row);
        var values = (row.ValueSource ?? row.Values).ToArray();
        ValidateValues(values: values);

        return new Row
        {
            Values = Array.AsReadOnly(array: values)
        };
    });
}