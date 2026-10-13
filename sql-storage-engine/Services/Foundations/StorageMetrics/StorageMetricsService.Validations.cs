// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;

namespace sql_storage_engine.Services.Foundations.StorageMetrics;
internal sealed partial class StorageMetricsService
{
    private void ValidateRecordBufferAccess(bool hit) =>
        Validate(inputs: [(Volatile.Read(location: ref state.IsClosed) != 0, "Storage metrics are closed.")]);

    private void ValidateRecordPageOperation(bool succeeded) =>
        Validate(inputs: [(Volatile.Read(location: ref state.IsClosed) != 0, "Storage metrics are closed.")]);

    private void ValidateRecordDuration(TimeSpan duration, bool succeeded) =>
        Validate(inputs: [(Volatile.Read(location: ref state.IsClosed) != 0, "Storage metrics are closed."), (duration < TimeSpan.Zero, "Metric duration cannot be negative.")]);

    private void ValidateRecordWalAppend(int bytes, bool succeeded) =>
        Validate(inputs: [(Volatile.Read(location: ref state.IsClosed) != 0, "Storage metrics are closed."), (bytes < 0, "Metric bytes cannot be negative.")]);

    private void ValidateRecordTransaction(string outcome) =>
        Validate(inputs: [(Volatile.Read(location: ref state.IsClosed) != 0, "Storage metrics are closed."), (outcome is not ("commit" or "rollback"), "Invalid transaction outcome.")]);

    private void ValidateMetricsOpen() =>
        Validate(inputs: [(Volatile.Read(location: ref state.IsClosed) != 0, "Storage metrics are closed.")]);

    private void ValidateSetBufferFrames(int pinned, int dirty) =>
        Validate(inputs: [(Volatile.Read(location: ref state.IsClosed) != 0, "Storage metrics are closed."), (pinned < 0, "Metric pinned cannot be negative."), (dirty < 0, "Metric dirty cannot be negative.")]);

    private void ValidateSetHeapBytes(long live, long dead) =>
        Validate(inputs: [(Volatile.Read(location: ref state.IsClosed) != 0, "Storage metrics are closed."), (live < 0, "Metric live cannot be negative."), (dead < 0, "Metric dead cannot be negative.")]);

    private void ValidateSetBTreeHeight(int height) =>
        Validate(inputs: [(Volatile.Read(location: ref state.IsClosed) != 0, "Storage metrics are closed."), (height < 0, "Metric height cannot be negative.")]);

    private void ValidateByteMetric(long bytes) =>
        Validate(inputs: [(Volatile.Read(location: ref state.IsClosed) != 0, "Storage metrics are closed."), (bytes < 0, "Metric bytes cannot be negative.")]);

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: input.Message);
            }
        }
    }
}