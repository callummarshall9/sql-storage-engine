// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Processings.StorageLimitSettings;
internal sealed partial class StorageLimitsProcessingService
{
    private static void ValidateStorageLimits(StorageLimits storageLimits)
    {
        ArgumentNullException.ThrowIfNull(argument: storageLimits);
        ValidateStorageLimits(bufferFrames: storageLimits.BufferFrames, rowBytes: storageLimits.RowBytes, keyBytes: storageLimits.KeyBytes, valueBytes: storageLimits.ValueBytes, overflowPages: storageLimits.OverflowPages, transactionDuration: storageLimits.TransactionDuration, undoBytes: storageLimits.UndoBytes, pinsPerTransaction: storageLimits.PinsPerTransaction, scanPages: storageLimits.ScanPages, concurrentTransactions: storageLimits.ConcurrentTransactions);
    }

    private static void ValidateStorageLimits(int bufferFrames, int rowBytes, int keyBytes, long valueBytes, int overflowPages, TimeSpan transactionDuration, long undoBytes, int pinsPerTransaction, int scanPages, int concurrentTransactions) =>
        Validate(inputs: [(IsOutOfRange(value: bufferFrames, maximum: StorageLimits.MaximumBufferFrames), nameof(bufferFrames)), (IsOutOfRange(value: rowBytes, maximum: StorageLimits.MaximumRowBytes), nameof(rowBytes)), (IsOutOfRange(value: keyBytes, maximum: StorageLimits.MaximumKeyBytes), nameof(keyBytes)), (IsOutOfRange(value: valueBytes, maximum: StorageLimits.MaximumValueBytes), nameof(valueBytes)), (IsOutOfRange(value: overflowPages, maximum: StorageLimits.MaximumOverflowPages), nameof(overflowPages)), (transactionDuration.Ticks < TimeSpan.TicksPerSecond || transactionDuration.Ticks > TimeSpan.TicksPerDay, nameof(transactionDuration)), (IsOutOfRange(value: undoBytes, maximum: StorageLimits.MaximumUndoBytes), nameof(undoBytes)), (IsOutOfRange(value: pinsPerTransaction, maximum: StorageLimits.MaximumPinsPerTransaction), nameof(pinsPerTransaction)), (IsOutOfRange(value: scanPages, maximum: StorageLimits.MaximumScanPages), nameof(scanPages)), (IsOutOfRange(value: concurrentTransactions, maximum: StorageLimits.MaximumConcurrentTransactions), nameof(concurrentTransactions))]);

    private static bool IsOutOfRange(long value, long maximum) =>
        value <= 0 || value > maximum;

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentOutOfRangeException(paramName: input.ParameterName, message: "Storage limit must be positive and within its published hard maximum.");
            }
        }
    }
}