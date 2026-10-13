// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Dependencies.PageFileSessions;

namespace sql_storage_engine.Brokers.PageFileSessions;
internal sealed class PageFileSessionBroker : IPageFileSessionBroker
{
    private static readonly PageFileSessionDependency dependency = new();
    public Guid OpenFile(string path, bool readOnly, bool createNew) =>
        dependency.OpenFile(path: path, readOnly: readOnly, createNew: createNew);

    public ValueTask ReadFileAsync(Guid sessionId, long offset, Memory<byte> destination, CancellationToken cancellationToken) =>
        dependency.ReadFileAsync(sessionId: sessionId, offset: offset, destination: destination, cancellationToken: cancellationToken);

    public ValueTask WriteFileAsync(Guid sessionId, long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        dependency.WriteFileAsync(sessionId: sessionId, offset: offset, source: source, cancellationToken: cancellationToken);

    public ValueTask FlushFileAsync(Guid sessionId, CancellationToken cancellationToken) =>
        dependency.FlushFileAsync(sessionId: sessionId, cancellationToken: cancellationToken);

    public void RestoreFileLength(Guid sessionId, long length) =>
        dependency.RestoreFileLength(sessionId: sessionId, length: length);

    public ValueTask CloseFileAsync(Guid sessionId) =>
        dependency.CloseFileAsync(sessionId: sessionId);

    public ValueTask EnterFileGateAsync(Guid sessionId, CancellationToken cancellationToken) =>
        dependency.EnterFileGateAsync(sessionId: sessionId, cancellationToken: cancellationToken);

    public void ExitFileGate(Guid sessionId) =>
        dependency.ExitFileGate(sessionId: sessionId);
}