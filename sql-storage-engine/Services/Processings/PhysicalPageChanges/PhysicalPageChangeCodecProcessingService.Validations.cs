// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.PhysicalPageChanges;
internal sealed partial class PhysicalPageChangeCodecProcessingService
{
    private static void ValidatePhysicalPageChange(PhysicalPageChange physicalPageChange)
    {
        ArgumentNullException.ThrowIfNull(argument: physicalPageChange);
        ValidateArguments(inputs: [(physicalPageChange.BeforeImage.Length != physicalPageChange.AfterImage.Length || !System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(physicalPageChange.AfterImage.Length)), nameof(physicalPageChange)), (!Enum.IsDefined(value: physicalPageChange.PageType) || physicalPageChange.PageType == PageType.Unknown, nameof(physicalPageChange.PageType))]);
    }

    private static void ValidatePhysicalPageChangeSource(ReadOnlySpan<byte> source)
    {
        Validate(inputs: [(source.Length < PhysicalPageChangeFormat.HeaderLength, "Physical page-change header is truncated.")]);

        var firstReserved = source.Slice(start: 10, length: 2)
            .IndexOfAnyExcept(value: (byte)0);

        var secondReserved = source.Slice(start: 20, length: 4)
            .IndexOfAnyExcept(value: (byte)0);

        Validate(inputs: [(firstReserved >= 0 || secondReserved >= 0, "Reserved physical-change bytes must be zero.")]);
        var before = BinaryPrimitives.ReadUInt32LittleEndian(source: source[12..]);
        var after = BinaryPrimitives.ReadUInt32LittleEndian(source: source[16..]);
        Validate(inputs: [(before != after || before > int.MaxValue || !System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)((int)before)) || (ulong)source.Length != PhysicalPageChangeFormat.HeaderLength + (ulong)before + after, "Physical page-change image lengths are invalid.")]);
        var type = (PageType)BinaryPrimitives.ReadUInt16LittleEndian(source: source[8..]);
        Validate(inputs: [(!Enum.IsDefined(value: type) || type == PageType.Unknown, "Invalid physical page type.")]);
    }

    private static void ValidateArguments(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "Physical images require one supported size and a known page type.", paramName: input.ParameterName);
            }
        }
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new StorageFormatException(message: input.Message);
            }
        }
    }
}