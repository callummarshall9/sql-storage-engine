// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Models.Logging;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.WalSegmentHeaders;
internal sealed partial class WalSegmentHeaderService
{
    private static void ValidateWalSegmentHeader(WalSegmentHeader walSegmentHeader)
    {
        ArgumentNullException.ThrowIfNull(argument: walSegmentHeader);
        Validate(inputs: [(walSegmentHeader.Timeline == 0, nameof(walSegmentHeader))]);
    }

    private static void ValidateWalSegmentHeaderSource(ReadOnlySpan<byte> source)
    {
        Validate(inputs: [(source.Length < WalFormatConstants.SegmentHeaderLength, false, "WAL segment header is truncated.")]);
        Validate(inputs: [(BinaryPrimitives.ReadUInt32LittleEndian(source: source) != WalFormatConstants.SegmentMagic, false, "Invalid WAL segment magic.")]);
        var version = BinaryPrimitives.ReadUInt16LittleEndian(source: source[4..]);
        Validate(inputs: [(version != WalFormatConstants.Version, false, $"Unsupported WAL version {version}.")]);

        var reservedPrefix = source.Slice(start: 6, length: 2)
            .IndexOfAnyExcept(value: (byte)0);

        var reservedSuffix = source.Slice(start: 40, length: 4)
            .IndexOfAnyExcept(value: (byte)0);

        Validate(inputs: [(reservedPrefix >= 0 || reservedSuffix >= 0, false, "Reserved WAL segment bytes must be zero.")]);
        Validate(inputs: [(BinaryPrimitives.ReadUInt32LittleEndian(source: source[44..]) != crc32.CalculateChecksum(bytes: source[..44]), true, "WAL segment-header checksum mismatch.")]);
        Validate(inputs: [(BinaryPrimitives.ReadUInt64LittleEndian(source: source[24..]) == 0, false, "WAL timeline must be nonzero.")]);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
            }
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