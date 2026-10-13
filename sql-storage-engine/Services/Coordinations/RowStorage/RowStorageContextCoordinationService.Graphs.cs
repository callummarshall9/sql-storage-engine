// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.RowStorage;
internal sealed partial class RowStorageContextCoordinationService
{
    public RowEncodingContext ProcessRowEncodingContextGraph(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateGraphEncoding(rowEncodingContext: rowEncodingContext);
        rowEncodingContext.Graph = graphs.ProcessGraphRowContext(graphRowContext: rowEncodingContext.Graph!);
        return rowEncodingContext;
    });

    private static void ValidateGraphEncoding(RowEncodingContext rowEncodingContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Graph);
    }
}