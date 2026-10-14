// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Logging;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.WalRecords;
internal sealed partial class WalRecordService
{
    private static void ValidateWalRecord(WalRecord walRecord)
    {
        ArgumentNullException.ThrowIfNull(argument: walRecord);
        ValidateWriteInputs(inputs: [(walRecord.Lsn.Value == 0 || walRecord.TransactionId.Value == 0, true, nameof(walRecord)), (!Enum.IsDefined(value: walRecord.Type), true, nameof(walRecord)), (walRecord.Payload.Length > WalFormatConstants.MaximumPayloadLength, false, nameof(walRecord))]);
    }

    private static void ValidateWalRecordSource(ReadOnlySpan<byte> source)
    {
        Validate(inputs: [(source.Length < WalFormatConstants.RecordHeaderLength, false, "WAL record is truncated.")]);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(source: source);
        Validate(inputs: [(length != source.Length || length > WalFormatConstants.RecordHeaderLength + WalFormatConstants.MaximumPayloadLength, false, "Invalid WAL record length.")]);
        var version = BinaryPrimitives.ReadUInt16LittleEndian(source: source[4..]);
        Validate(inputs: [(version != WalFormatConstants.Version, false, $"Unsupported WAL record version {version}.")]);
        var type = (WalRecordType)BinaryPrimitives.ReadUInt16LittleEndian(source: source[6..]);
        Validate(inputs: [(!Enum.IsDefined(value: type), false, $"Unknown WAL record type {(ushort)type}.")]);
        Validate(inputs: [(BinaryPrimitives.ReadUInt32LittleEndian(source: source[32..]) != 0, false, "Reserved WAL record bytes must be zero.")]);
        Validate(inputs: [(BinaryPrimitives.ReadUInt32LittleEndian(source: source[36..]) != crc32.CalculateChecksumWithZeroedField(bytes: source, zeroOffset: 36), true, "WAL record checksum mismatch.")]);
    }

    private static void ValidateWriteInputs(ReadOnlySpan<(bool Invalid, bool Range, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (!input.Invalid)
            {
                continue;
            }

            if (input.Range)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
            }

            throw new ArgumentException(message: "WAL payload exceeds its bound.", paramName: input.ParameterName);
        }
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, bool Corrupt, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (!input.Invalid)
            {
                continue;
            }

            if (input.Corrupt)
            {
                throw new StorageCorruptionException(message: input.Message);
            }

            throw new StorageFormatException(message: input.Message);
        }
    }
}