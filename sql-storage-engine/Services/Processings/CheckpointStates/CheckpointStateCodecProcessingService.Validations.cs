// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.CheckpointStates;
internal sealed partial class CheckpointStateCodecProcessingService
{
    private static void ValidateCheckpointState(CheckpointState checkpointState)
    {
        ArgumentNullException.ThrowIfNull(argument: checkpointState);
        ArgumentNullException.ThrowIfNull(argument: checkpointState.ActiveTransactions);
        ArgumentNullException.ThrowIfNull(argument: checkpointState.DirtyPages);
        ValidateArguments(inputs: [(checkpointState.ActiveTransactions.Count > CheckpointFormat.MaximumEntries, nameof(checkpointState.ActiveTransactions)), (checkpointState.DirtyPages.Count > CheckpointFormat.MaximumEntries, nameof(checkpointState.DirtyPages))]);
    }

    private static void ValidateCheckpointStateSource(ReadOnlySpan<byte> source)
    {
        Validate(inputs: [(source.Length < CheckpointFormat.HeaderLength, "Checkpoint is truncated.")]);
        Validate(inputs: [(BinaryPrimitives.ReadUInt16LittleEndian(source: source) != CheckpointFormat.Version, "Unsupported checkpoint version.")]);

        var reserved = source.Slice(start: 2, length: 6)
            .IndexOfAnyExcept(value: (byte)0);

        Validate(inputs: [(reserved >= 0, "Reserved checkpoint bytes must be zero.")]);
        var active = BinaryPrimitives.ReadUInt32LittleEndian(source: source[16..]);
        var dirty = BinaryPrimitives.ReadUInt32LittleEndian(source: source[20..]);
        Validate(inputs: [(active > CheckpointFormat.MaximumEntries || dirty > CheckpointFormat.MaximumEntries || (ulong)source.Length != CheckpointFormat.HeaderLength + ((ulong)active + dirty) * 16, "Checkpoint counts or length are invalid.")]);
    }

    private static void ValidateArguments(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName);
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