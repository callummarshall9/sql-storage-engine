// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Configurations;
using System.Threading;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Brokers.Metrics;

namespace sql_storage_engine.Services.Foundations.StorageMetrics;
internal sealed partial class StorageMetricsService : IStorageMetricsService
{
    private readonly StorageMetricsBroker broker;
    private readonly StorageMetricState state;
    public StorageMetricsService(StorageMetricsConfigurationModel configuration)
    {
        broker = new StorageMetricsBroker(configuration: configuration);
        state = new StorageMetricState();
    }

    public void RecordBufferAccess(bool hit) =>
        TryCatch(operation: () =>
    {
        ValidateRecordBufferAccess(hit: hit);
        broker.RecordBufferAccess(hit: hit);
    });

    public void RecordPageRead(bool succeeded) =>
        TryCatch(operation: () =>
    {
        ValidateRecordPageOperation(succeeded: succeeded);
        broker.RecordPageRead(succeeded: succeeded);
    });

    public void RecordPageWrite(bool succeeded) =>
        TryCatch(operation: () =>
    {
        ValidateRecordPageOperation(succeeded: succeeded);
        broker.RecordPageWrite(succeeded: succeeded);
    });

    public void RecordPageFlush(TimeSpan duration, bool succeeded) =>
        TryCatch(operation: () =>
    {
        ValidateRecordDuration(duration: duration, succeeded: succeeded);
        broker.RecordPageFlush(duration: duration, succeeded: succeeded);
    });

    public void RecordWalAppend(int bytes, bool succeeded) =>
        TryCatch(operation: () =>
    {
        ValidateRecordWalAppend(bytes: bytes, succeeded: succeeded);
        broker.RecordWalAppend(bytes: bytes, succeeded: succeeded);
    });

    public void RecordWalFlush(TimeSpan duration, bool succeeded) =>
        TryCatch(operation: () =>
    {
        ValidateRecordDuration(duration: duration, succeeded: succeeded);
        broker.RecordWalFlush(duration: duration, succeeded: succeeded);
    });

    public void RecordTransaction(string outcome) =>
        TryCatch(operation: () =>
    {
        ValidateRecordTransaction(outcome: outcome);
        broker.RecordTransaction(outcome: outcome);
    });

    public void RecordDeadlock() =>
        TryCatch(operation: () =>
    {
        ValidateMetricsOpen();
        broker.RecordDeadlock();
    });

    public void RecordBTreeSplit() =>
        TryCatch(operation: () =>
    {
        ValidateMetricsOpen();
        broker.RecordBTreeSplit();
    });

    public void RecordBTreeMerge() =>
        TryCatch(operation: () =>
    {
        ValidateMetricsOpen();
        broker.RecordBTreeMerge();
    });

    public void RecordCheckpoint(TimeSpan duration, bool succeeded) =>
        TryCatch(operation: () =>
    {
        ValidateRecordDuration(duration: duration, succeeded: succeeded);
        broker.RecordCheckpoint(duration: duration, succeeded: succeeded);
    });

    public void SetBufferFrames(int pinned, int dirty) =>
        TryCatch(operation: () =>
    {
        ValidateSetBufferFrames(pinned: pinned, dirty: dirty);
        broker.SetBufferFrames(pinned: pinned, dirty: dirty);
    });

    public void SetHeapBytes(long live, long dead) =>
        TryCatch(operation: () =>
    {
        ValidateSetHeapBytes(live: live, dead: dead);
        broker.SetHeapBytes(live: live, dead: dead);
    });

    public void SetBTreeHeight(int height) =>
        TryCatch(operation: () =>
    {
        ValidateSetBTreeHeight(height: height);
        broker.SetBTreeHeight(height: height);
    });

    public void SetOverflowBytes(long bytes) =>
        TryCatch(operation: () =>
    {
        ValidateByteMetric(bytes: bytes);
        broker.SetOverflowBytes(bytes: bytes);
    });

    public void SetRecoveryDistance(long bytes) =>
        TryCatch(operation: () =>
    {
        ValidateByteMetric(bytes: bytes);
        broker.SetRecoveryDistance(bytes: bytes);
    });

    public void CloseStorageMetrics() =>
        TryCatch(operation: () =>
    {
        if (Interlocked.Exchange(location1: ref state.IsClosed, value: 1) == 0)
        {
            broker.CloseStorageMetrics();
        }
    });
}