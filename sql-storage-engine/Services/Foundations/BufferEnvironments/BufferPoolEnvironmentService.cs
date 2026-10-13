// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Buffers;
using sql_storage_engine.Brokers.BufferEnvironments;

namespace sql_storage_engine.Services.Foundations.BufferEnvironments;
internal sealed partial class BufferPoolEnvironmentService : IBufferPoolEnvironmentService
{
    private readonly BufferPoolEnvironmentBroker broker;
    public BufferPoolEnvironmentService(BufferPoolConfiguration bufferPoolConfiguration)
    {
        broker = OpenBufferPoolEnvironmentBroker(bufferPoolConfiguration: bufferPoolConfiguration);
    }

    private static BufferPoolEnvironmentBroker OpenBufferPoolEnvironmentBroker(BufferPoolConfiguration bufferPoolConfiguration) =>
        TryCatch(operation: () =>
    {
        ValidateConfiguration(bufferPoolContext: new BufferPoolContext { Configuration = bufferPoolConfiguration });
        return new BufferPoolEnvironmentBroker(bufferPoolConfiguration: bufferPoolConfiguration);
    });

    public BufferPoolContext CreateBufferPoolContext(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(bufferPoolContext: bufferPoolContext);

        return bufferPoolContext with
        {
            PageSize = broker.ReadPageSize()
        };
    });

    public void EnterBufferPoolContext(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(bufferPoolContext: bufferPoolContext);
        broker.Enter();
    });

    public ValueTask EnterBufferPoolContextAsync(BufferPoolContext bufferPoolContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(bufferPoolContext: bufferPoolContext, cancellationToken: cancellationToken);

        await broker.EnterAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void ExitBufferPoolContext(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(bufferPoolContext: bufferPoolContext);
        broker.Exit();
    });

    public void RecordBufferPoolContextHit(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(bufferPoolContext: bufferPoolContext);
        broker.RecordHit();
    });

    public void RecordBufferPoolContextMiss(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(bufferPoolContext: bufferPoolContext);
        broker.RecordMiss();
    });

    public ValueTask ReadBufferPoolContextAsync(BufferPoolContext bufferPoolContext, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRead(bufferPoolContext: bufferPoolContext, pageId: pageId, destination: destination, cancellationToken: cancellationToken);

        await broker.ReadAsync(pageId: pageId, destination: destination, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WriteBufferPoolContextAsync(BufferPoolContext bufferPoolContext, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(bufferPoolContext: bufferPoolContext, pageId: pageId, source: source, cancellationToken: cancellationToken);

        await broker.WriteAsync(pageId: pageId, source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FlushBufferPoolContextAsync(BufferPoolContext bufferPoolContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(bufferPoolContext: bufferPoolContext, cancellationToken: cancellationToken);

        await broker.FlushAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask GuardBufferPoolContextAsync(BufferPoolContext bufferPoolContext, ulong pageId, ulong logSequenceNumber, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateGuard(bufferPoolContext: bufferPoolContext, pageId: pageId, logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken);

        await broker.GuardAsync(pageId: pageId, logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask CloseBufferPoolContextAsync(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: async () =>
    {
        ValidateContext(bufferPoolContext: bufferPoolContext);

        await broker.CloseAsync()
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public BufferPoolContext ReadBufferPoolContextLifetime(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(bufferPoolContext: bufferPoolContext);
        ArgumentNullException.ThrowIfNull(argument: bufferPoolContext.PinRelease);

        return bufferPoolContext with
        {
            Lifetime = broker.CreatePinLifetime(release: bufferPoolContext.PinRelease)
        };
    });
}