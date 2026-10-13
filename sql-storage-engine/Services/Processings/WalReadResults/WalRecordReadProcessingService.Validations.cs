// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.WalReadResults;
internal sealed partial class WalRecordReadProcessingService
{
    private static void ValidateWalReadResultSource(ReadOnlySpan<byte> source)
    {
        if (source.Length >= sizeof(uint))
        {
            ValidateRecordLength(length: BinaryPrimitives.ReadUInt32LittleEndian(source: source));
        }
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

    private static void ValidateWalReadResult(WalReadResult walReadResult)
    {
        ArgumentNullException.ThrowIfNull(argument: walReadResult);
        ArgumentNullException.ThrowIfNull(argument: walReadResult.Records);
    }
}