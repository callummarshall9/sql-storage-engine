// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Services.Foundations.StorageMetrics;

namespace sql_storage_engine.Exposures.Diagnostics;
public sealed class StorageMetricsExposure : IDisposable
{
    private readonly StorageMetricsService service;
    public StorageMetricsExposure(string? meterVersion = null)
    {
        service = new StorageMetricsService(configuration: new StorageMetricsConfigurationModel { MeterVersion = meterVersion });
    }

    public void RecordBufferAccess(bool hit) =>
        service.RecordBufferAccess(hit: hit);

    public void RecordPageRead(bool succeeded) =>
        service.RecordPageRead(succeeded: succeeded);

    public void RecordPageWrite(bool succeeded) =>
        service.RecordPageWrite(succeeded: succeeded);

    public void RecordPageFlush(TimeSpan duration, bool succeeded) =>
        service.RecordPageFlush(duration: duration, succeeded: succeeded);

    public void RecordWalAppend(int bytes, bool succeeded) =>
        service.RecordWalAppend(bytes: bytes, succeeded: succeeded);

    public void RecordWalFlush(TimeSpan duration, bool succeeded) =>
        service.RecordWalFlush(duration: duration, succeeded: succeeded);

    public void RecordTransaction(string outcome) =>
        service.RecordTransaction(outcome: outcome);

    public void RecordDeadlock() =>
        service.RecordDeadlock();

    public void RecordBTreeSplit() =>
        service.RecordBTreeSplit();

    public void RecordBTreeMerge() =>
        service.RecordBTreeMerge();

    public void RecordCheckpoint(TimeSpan duration, bool succeeded) =>
        service.RecordCheckpoint(duration: duration, succeeded: succeeded);

    public void SetBufferFrames(int pinned, int dirty) =>
        service.SetBufferFrames(pinned: pinned, dirty: dirty);

    public void SetHeapBytes(long live, long dead) =>
        service.SetHeapBytes(live: live, dead: dead);

    public void SetBTreeHeight(int height) =>
        service.SetBTreeHeight(height: height);

    public void SetOverflowBytes(long bytes) =>
        service.SetOverflowBytes(bytes: bytes);

    public void SetRecoveryDistance(long bytes) =>
        service.SetRecoveryDistance(bytes: bytes);

    public void Dispose() =>
        service.CloseStorageMetrics();
}