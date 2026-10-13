// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;

namespace sql_storage_engine.Exposures.Buffers;
/// <summary>
/// Holds a page resident in the buffer pool. The handle and its memory must not be used after disposal.
/// </summary>
public interface IPinnedPageExposure : IDisposable
{
    PageId ReadPageId();

    Memory<byte> ReadMemory();

    LogSequenceNumber ReadPageLogSequenceNumber();

    bool ReadIsDirty();
    /// <summary>Marks the page dirty at the supplied log position.</summary>
    void MarkDirty(LogSequenceNumber logSequenceNumber);
}