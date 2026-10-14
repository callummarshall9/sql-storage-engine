// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.WalRecordContexts;
internal sealed partial class WalRecordWalAppendContextCodecProcessingService
{
    private static void ValidateRecord(WalAppendContext walAppendContext)
    {
        ArgumentNullException.ThrowIfNull(argument: walAppendContext);
        ArgumentNullException.ThrowIfNull(argument: walAppendContext.Record);
    }

    private static void ValidateSource(WalAppendContext walAppendContext)
    {
        ArgumentNullException.ThrowIfNull(argument: walAppendContext);
        ArgumentNullException.ThrowIfNull(argument: walAppendContext.Source);
    }

    private static void ValidateRecordLength(uint length) =>
        Validate(inputs: [(length < WalFormatConstants.RecordHeaderLength || length > WalFormatConstants.RecordHeaderLength + WalFormatConstants.MaximumPayloadLength, "Invalid WAL record length within the log.")]);

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new StorageCorruptionException(message: input.Message);
            }
        }
    }
}