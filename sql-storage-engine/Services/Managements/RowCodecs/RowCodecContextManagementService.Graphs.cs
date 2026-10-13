// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Managements.RowCodecs;
internal sealed partial class RowCodecContextManagementService
{
    public RowEncodingContext ProcessRowEncodingContextGraph(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateGraphEncoding(rowEncodingContext: rowEncodingContext);
        return storage.ProcessRowEncodingContextGraph(rowEncodingContext: rowEncodingContext);
    });

    private static void ValidateGraphEncoding(RowEncodingContext rowEncodingContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext);
        ArgumentNullException.ThrowIfNull(argument: rowEncodingContext.Graph);
    }
}