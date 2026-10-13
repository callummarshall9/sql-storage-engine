// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Foundations.StorageMetrics;
internal interface IStorageMetricsService
{
    void RecordBufferAccess(bool hit);

    void RecordPageRead(bool succeeded);

    void RecordPageWrite(bool succeeded);

    void RecordPageFlush(TimeSpan duration, bool succeeded);

    void RecordWalAppend(int bytes, bool succeeded);

    void RecordWalFlush(TimeSpan duration, bool succeeded);

    void RecordTransaction(string outcome);

    void RecordDeadlock();

    void RecordBTreeSplit();

    void RecordBTreeMerge();

    void RecordCheckpoint(TimeSpan duration, bool succeeded);

    void SetBufferFrames(int pinned, int dirty);

    void SetHeapBytes(long live, long dead);

    void SetBTreeHeight(int height);

    void SetOverflowBytes(long bytes);

    void SetRecoveryDistance(long bytes);

    void CloseStorageMetrics();
}