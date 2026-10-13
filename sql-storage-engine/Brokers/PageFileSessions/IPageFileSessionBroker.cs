// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.PageFileSessions;
internal interface IPageFileSessionBroker
{
    Guid OpenFile(string path, bool readOnly, bool createNew);

    ValueTask ReadFileAsync(Guid sessionId, long offset, Memory<byte> destination, CancellationToken cancellationToken);

    ValueTask WriteFileAsync(Guid sessionId, long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask FlushFileAsync(Guid sessionId, CancellationToken cancellationToken);

    void RestoreFileLength(Guid sessionId, long length);

    ValueTask CloseFileAsync(Guid sessionId);

    ValueTask EnterFileGateAsync(Guid sessionId, CancellationToken cancellationToken);

    void ExitFileGate(Guid sessionId);
}