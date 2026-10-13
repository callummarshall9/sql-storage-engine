// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.RowPayloads;
internal sealed partial class RowScalarPayloadOrchestrationService
{
    private static void ValidateContext(RowPayloadContext rowPayloadContext)
    {
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Column);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Column.Type);
    }

    private static void ValidateValue(RowPayloadContext rowPayloadContext)
    {
        ValidateContext(rowPayloadContext: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Value);
    }

    private static void ValidateSource(RowPayloadContext rowPayloadContext)
    {
        ValidateContext(rowPayloadContext: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Source);
    }

    private static void ValidateEncryption(RowPayloadContext rowPayloadContext)
    {
        ValidateSource(rowPayloadContext: rowPayloadContext);
        ArgumentNullException.ThrowIfNull(argument: rowPayloadContext.Column!.Encryption);
    }
}