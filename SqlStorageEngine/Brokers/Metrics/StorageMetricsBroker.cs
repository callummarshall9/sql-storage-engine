// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Dependencies.Metrics;

namespace sql_storage_engine.Brokers.Metrics;
internal sealed class StorageMetricsBroker : IStorageMetricsBroker
{
    private readonly StorageMetricsDependency dependency;
    public StorageMetricsBroker(StorageMetricsConfigurationModel configuration)
    {
        dependency = new StorageMetricsDependency(configuration.MeterVersion);
    }

    public void RecordBufferAccess(bool hit) =>
        dependency.RecordBufferAccess(hit: hit);

    public void RecordPageRead(bool succeeded) =>
        dependency.RecordPageRead(succeeded: succeeded);

    public void RecordPageWrite(bool succeeded) =>
        dependency.RecordPageWrite(succeeded: succeeded);

    public void RecordPageFlush(TimeSpan duration, bool succeeded) =>
        dependency.RecordPageFlush(duration: duration, succeeded: succeeded);

    public void RecordWalAppend(int bytes, bool succeeded) =>
        dependency.RecordWalAppend(bytes: bytes, succeeded: succeeded);

    public void RecordWalFlush(TimeSpan duration, bool succeeded) =>
        dependency.RecordWalFlush(duration: duration, succeeded: succeeded);

    public void RecordTransaction(string outcome) =>
        dependency.RecordTransaction(outcome: outcome);

    public void RecordDeadlock() =>
        dependency.RecordDeadlock();

    public void RecordBTreeSplit() =>
        dependency.RecordBTreeSplit();

    public void RecordBTreeMerge() =>
        dependency.RecordBTreeMerge();

    public void RecordCheckpoint(TimeSpan duration, bool succeeded) =>
        dependency.RecordCheckpoint(duration: duration, succeeded: succeeded);

    public void SetBufferFrames(int pinned, int dirty) =>
        dependency.SetBufferFrames(pinned: pinned, dirty: dirty);

    public void SetHeapBytes(long live, long dead) =>
        dependency.SetHeapBytes(live: live, dead: dead);

    public void SetBTreeHeight(int height) =>
        dependency.SetBTreeHeight(height: height);

    public void SetOverflowBytes(long bytes) =>
        dependency.SetOverflowBytes(bytes: bytes);

    public void SetRecoveryDistance(long bytes) =>
        dependency.SetRecoveryDistance(bytes: bytes);

    public void CloseStorageMetrics() =>
        dependency.Dispose();
}