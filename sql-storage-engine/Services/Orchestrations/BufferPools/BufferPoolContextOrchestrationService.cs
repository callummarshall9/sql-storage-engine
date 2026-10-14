// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Buffers;
using sql_storage_engine.Services.Foundations.BufferEnvironments;
using sql_storage_engine.Services.Foundations.BufferFrames;
using sql_storage_engine.Services.Foundations.PageHeaders;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.BufferPools;
internal sealed partial class BufferPoolContextOrchestrationService : IBufferPoolContextOrchestrationService
{
    private readonly BufferPoolEnvironmentService environment;
    private readonly BufferFrameService frames;
    private readonly PageHeaderService headers;
    private readonly BufferPoolContext state;
    private BufferPoolContextOrchestrationService(BufferPoolConfiguration bufferPoolConfiguration)
    {
        environment = new BufferPoolEnvironmentService(bufferPoolConfiguration: bufferPoolConfiguration);
        frames = new BufferFrameService();
        headers = new PageHeaderService();
        state = PrepareBufferPoolContext(bufferPoolContext: new BufferPoolContext { Configuration = bufferPoolConfiguration });
    }

    internal static BufferPoolContextOrchestrationService CreateBufferPoolContextOrchestrationService(BufferPoolConfiguration bufferPoolConfiguration) =>
        TryCatch(operation: () =>
    {
        ValidateCreation(bufferPoolContext: new BufferPoolContext { Configuration = bufferPoolConfiguration });
        return new BufferPoolContextOrchestrationService(bufferPoolConfiguration: bufferPoolConfiguration);
    });

    private BufferPoolContext PrepareBufferPoolContext(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidateCreation(bufferPoolContext: bufferPoolContext);
        var context = environment.CreateBufferPoolContext(bufferPoolContext: bufferPoolContext);

        return context with
        {
            Runtime = new BufferPoolRuntime
            {
                Frames = new Dictionary<ulong, BufferFrameState>(),
                Clock = new List<BufferFrameState>()
            }
        };
    });

    public BufferPoolContext ReadBufferPoolContext() =>
        TryCatch(operation: () =>
    {
        ValidateState();
        return state;
    });

    public BufferPoolContext ReadBufferPoolContextMetrics() =>
        TryCatch(operation: () =>
    {
        ValidateState();

        return state with
        {
            HitCount = Interlocked.Read(location: ref state.Runtime!.HitCount),
            MissCount = Interlocked.Read(location: ref state.Runtime!.MissCount)
        };
    });

    public BufferPoolContext ReadBufferPoolContextCache() =>
        TryCatch(operation: () =>
    {
        ValidateState();
        environment.EnterBufferPoolContext(bufferPoolContext: state);

        try
        {
            return state with
            {
                FrameCount = state.Runtime!.Frames!.Count,
                PinnedPageCount = state.Runtime.Frames.Values.Count(predicate: item => !frames.ReadBufferFrameStateIsEvictable(bufferFrameState: item, pinId: null))
            };
        }
        finally
        {
            environment.ExitBufferPoolContext(bufferPoolContext: state);
        }
    });

    private ValueTask<BufferPoolContext> ReadCoreBufferPoolContextPageAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);
        ThrowIfDisposedCore();

        await environment.EnterBufferPoolContextAsync(bufferPoolContext: state, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            ThrowIfDisposedCore();

            if (state.Runtime!.Frames!.TryGetValue(key: pageId, value: out var existing))
            {
                Interlocked.Increment(location: ref state.Runtime.HitCount);
                environment.RecordBufferPoolContextHit(bufferPoolContext: state);

                return state with
                {
                    ResultFrame = existing,
                    ResultPinId = frames.AcquireBufferFrameStatePin(bufferFrameState: existing, pinId: null)
                };
            }

            Interlocked.Increment(location: ref state.Runtime.MissCount);
            environment.RecordBufferPoolContextMiss(bufferPoolContext: state);
            var bytes = new byte[state.PageSize];
            BufferFrameState? victim = null;

            if (state.Runtime.Frames.Count == state.Configuration!.Capacity)
            {
                victim = SelectVictimCore();
            }

            if (victim is not null)
            {
                await FlushFrameAsyncCore(bufferFrameState: victim, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            await environment.ReadBufferPoolContextAsync(bufferPoolContext: state, pageId: pageId, destination: bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            BufferFrameState loaded;

            if (victim is null)
            {
                loaded = frames.PrepareBufferFrameState(bufferFrameState: new BufferFrameState { PageId = pageId, Bytes = bytes });
                state.Runtime.Clock!.Add(item: loaded);
            }
            else
            {
                state.Runtime.Frames.Remove(key: frames.ReadBufferFrameStatePageId(bufferFrameState: victim, pinId: null));
                frames.ResetBufferFrameState(bufferFrameState: victim, pageId: pageId, bytes: bytes);
                loaded = victim;
            }

            state.Runtime.Frames.Add(key: pageId, value: loaded);

            return state with
            {
                ResultFrame = loaded,
                ResultPinId = frames.AcquireBufferFrameStatePin(bufferFrameState: loaded, pinId: null)
            };
        }
        finally
        {
            environment.ExitBufferPoolContext(bufferPoolContext: state);
        }
    });

    public ValueTask FlushBufferPoolContextPageAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        await FlushPageAsyncCore(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FlushBufferPoolContextAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(cancellationToken: cancellationToken);

        await FlushAllAsyncCore(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask DiscardBufferPoolContextPageAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        await DiscardPageAsyncCore(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask DiscardBufferPoolContextAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(cancellationToken: cancellationToken);

        await DiscardAllAsyncCore(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask CloseBufferPoolContextAsync(BufferPoolContext? bufferPoolContext) =>
        TryCatch(operation: async () =>
    {
        ValidateClose(bufferPoolContext: bufferPoolContext);

        if (bufferPoolContext is not null)
        {
            frames.ReleaseBufferFrameStatePin(bufferFrameState: bufferPoolContext.ResultFrame!, pinId: bufferPoolContext.ResultPinId);
            return;
        }

        await DisposeAsyncCore()
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    private async ValueTask FlushPageAsyncCore(ulong pageId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposedCore();

        await environment.EnterBufferPoolContextAsync(bufferPoolContext: state, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            ThrowIfDisposedCore();

            if (!state.Runtime!.Frames!.TryGetValue(key: pageId, value: out var bufferFrameState))
            {
                throw new StorageResourceException(message: $"Cannot flush uncached {pageId}.", innerException: new KeyNotFoundException());
            }

            await FlushFrameAsyncCore(bufferFrameState: bufferFrameState, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await environment.FlushBufferPoolContextAsync(bufferPoolContext: state, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            environment.ExitBufferPoolContext(bufferPoolContext: state);
        }
    }

    private async ValueTask FlushAllAsyncCore(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposedCore();

        await environment.EnterBufferPoolContextAsync(bufferPoolContext: state, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            ThrowIfDisposedCore();

            foreach (BufferFrameState bufferFrameState in state.Runtime!.Clock!)
            {
                await FlushFrameAsyncCore(bufferFrameState: bufferFrameState, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            await environment.FlushBufferPoolContextAsync(bufferPoolContext: state, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            environment.ExitBufferPoolContext(bufferPoolContext: state);
        }
    }

    private async ValueTask DiscardPageAsyncCore(ulong pageId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposedCore();

        await environment.EnterBufferPoolContextAsync(bufferPoolContext: state, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            ThrowIfDisposedCore();

            if (!state.Runtime!.Frames!.TryGetValue(key: pageId, value: out var bufferFrameState))
            {
                return;
            }

            if (!frames.ReadBufferFrameStateIsEvictable(bufferFrameState: bufferFrameState, pinId: null))
            {
                throw new StorageResourceException(message: $"Cannot discard pinned {pageId}.", innerException: new InvalidOperationException());
            }

            state.Runtime!.Frames!.Remove(key: pageId);
            var index = state.Runtime!.Clock!.IndexOf(item: bufferFrameState);

            if (index >= 0)
            {
                state.Runtime!.Clock!.RemoveAt(index: index);

                if (state.Runtime!.Clock!.Count == 0)
                {
                    state.Runtime!.ClockHand = 0;
                }
                else
                {
                    if (index < state.Runtime!.ClockHand)
                    {
                        state.Runtime!.ClockHand--;
                    }

                    state.Runtime!.ClockHand %= state.Runtime!.Clock!.Count;
                }
            }
        }
        finally
        {
            environment.ExitBufferPoolContext(bufferPoolContext: state);
        }
    }

    private async ValueTask DiscardAllAsyncCore(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposedCore();

        await environment.EnterBufferPoolContextAsync(bufferPoolContext: state, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            if (state.Runtime!.Frames!.Values.Any(predicate: bufferFrameState => !frames.ReadBufferFrameStateIsEvictable(bufferFrameState: bufferFrameState, pinId: null)))
            {
                throw new StorageResourceException(message: "Cannot discard the buffer pool while pages are pinned.", innerException: new InvalidOperationException());
            }

            state.Runtime!.Frames!.Clear();
            state.Runtime!.Clock!.Clear();
            state.Runtime!.ClockHand = 0;
        }
        finally
        {
            environment.ExitBufferPoolContext(bufferPoolContext: state);
        }
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (state.Runtime!.Disposed)
        {
            return;
        }

        await environment.EnterBufferPoolContextAsync(bufferPoolContext: state, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            if (state.Runtime!.Disposed)
            {
                return;
            }

            if (state.Runtime!.Frames!.Values.Any(predicate: bufferFrameState => !frames.ReadBufferFrameStateIsEvictable(bufferFrameState: bufferFrameState, pinId: null)))
            {
                throw new StorageResourceException(message: "Cannot dispose a buffer pool while pages are pinned.", innerException: new InvalidOperationException());
            }

            foreach (BufferFrameState bufferFrameState in state.Runtime!.Clock!)
            {
                await FlushFrameAsyncCore(bufferFrameState: bufferFrameState, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            await environment.FlushBufferPoolContextAsync(bufferPoolContext: state, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

            state.Runtime!.Disposed = true;
            state.Runtime!.Frames!.Clear();
            state.Runtime!.Clock!.Clear();
        }
        finally
        {
            environment.ExitBufferPoolContext(bufferPoolContext: state);
        }

        await environment.CloseBufferPoolContextAsync(bufferPoolContext: state)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private void ThrowIfDisposedCore() =>
        ObjectDisposedException.ThrowIf(condition: state.Runtime!.Disposed, instance: this);

    private BufferFrameState SelectVictimCore()
    {
        for (var inspected = 0; inspected < state.Runtime!.Clock!.Count * 2; inspected++)
        {
            BufferFrameState candidate = state.Runtime!.Clock![state.Runtime!.ClockHand];
            state.Runtime!.ClockHand = (state.Runtime!.ClockHand + 1) % state.Runtime!.Clock!.Count;

            if (!frames.GiveBufferFrameStateSecondChance(bufferFrameState: candidate))
            {
                return candidate;
            }
        }

        throw new StorageResourceExhaustedException(message: $"Buffer pool capacity {state.Configuration!.Capacity} is exhausted because every bufferFrameState is pinned.");
    }

    private async ValueTask FlushFrameAsyncCore(BufferFrameState bufferFrameState, CancellationToken cancellationToken)
    {
        if (!frames.ReadBufferFrameStateIsDirty(bufferFrameState: bufferFrameState, pinId: null))
        {
            return;
        }

        var snapshot = frames.CaptureBufferFrameStateFlushSnapshot(bufferFrameState: bufferFrameState);
        cancellationToken.ThrowIfCancellationRequested();

        await environment.GuardBufferPoolContextAsync(bufferPoolContext: state, pageId: frames.ReadBufferFrameStatePageId(bufferFrameState: bufferFrameState, pinId: null), logSequenceNumber: snapshot.PageLogSequenceNumber, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        headers.WriteChecksum(page: snapshot.Bytes, pageSize: state.PageSize);

        await environment.WriteBufferPoolContextAsync(bufferPoolContext: state, pageId: frames.ReadBufferFrameStatePageId(bufferFrameState: bufferFrameState, pinId: null), source: snapshot.Bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        frames.CompleteBufferFrameStateFlush(bufferFrameState: bufferFrameState, dirtyGeneration: snapshot.DirtyGeneration);
    }

    public ulong ReadBufferPoolContextPinPageId(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidatePin(bufferPoolContext: bufferPoolContext);
        return frames.ReadBufferFrameStatePageId(bufferFrameState: bufferPoolContext.ResultFrame!, pinId: bufferPoolContext.ResultPinId);
    });

    public Memory<byte> ReadBufferPoolContextPinMemory(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidatePin(bufferPoolContext: bufferPoolContext);
        return frames.ReadBufferFrameStateMemory(bufferFrameState: bufferPoolContext.ResultFrame!, pinId: bufferPoolContext.ResultPinId);
    });

    public ulong ReadBufferPoolContextPinLogSequenceNumber(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidatePin(bufferPoolContext: bufferPoolContext);
        return frames.ReadBufferFrameStatePageLogSequenceNumber(bufferFrameState: bufferPoolContext.ResultFrame!, pinId: bufferPoolContext.ResultPinId);
    });

    public bool ReadBufferPoolContextPinIsDirty(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidatePin(bufferPoolContext: bufferPoolContext);
        return frames.ReadBufferFrameStateIsDirty(bufferFrameState: bufferPoolContext.ResultFrame!, pinId: bufferPoolContext.ResultPinId);
    });

    private void MarkCoreBufferPoolContextPinDirty(BufferPoolContext bufferPoolContext, ulong logSequenceNumber) =>
        TryCatch(operation: () =>
    {
        ValidatePinMutation(bufferPoolContext: bufferPoolContext, logSequenceNumber: logSequenceNumber);
        frames.MarkBufferFrameStateDirty(bufferFrameState: bufferPoolContext.ResultFrame!, pageLogSequenceNumber: logSequenceNumber, pinId: bufferPoolContext.ResultPinId);
    });

    private void ReleaseCoreBufferPoolContextPin(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidatePin(bufferPoolContext: bufferPoolContext);
        frames.ReleaseBufferFrameStatePin(bufferFrameState: bufferPoolContext.ResultFrame!, pinId: bufferPoolContext.ResultPinId);
    });

    public ValueTask<BufferPoolContext> ReadBufferPoolContextPageAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch<BufferPoolContext>(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        return await ReadCoreBufferPoolContextPageAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BufferPoolContext> ReadBufferPoolContextLeaseAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch<BufferPoolContext>(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        var pinned = await ReadCoreBufferPoolContextPageAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            return environment.ReadBufferPoolContextLifetime(bufferPoolContext: pinned with { PinRelease = () => ReleaseCoreBufferPoolContextPin(bufferPoolContext: pinned), PinDirty = lsn => MarkCoreBufferPoolContextPinDirty(bufferPoolContext: pinned, logSequenceNumber: lsn) });
        }
        catch
        {
            ReleaseCoreBufferPoolContextPin(bufferPoolContext: pinned);
            throw;
        }
    });

    public void MarkBufferPoolContextPinDirty(BufferPoolContext bufferPoolContext, ulong logSequenceNumber) =>
        TryCatch(operation: () =>
    {
        ValidatePinMutation(bufferPoolContext: bufferPoolContext, logSequenceNumber: logSequenceNumber);
        MarkCoreBufferPoolContextPinDirty(bufferPoolContext: bufferPoolContext, logSequenceNumber: logSequenceNumber);
    });

    public void ReleaseBufferPoolContextPin(BufferPoolContext bufferPoolContext) =>
        TryCatch(operation: () =>
    {
        ValidatePin(bufferPoolContext: bufferPoolContext);
        ReleaseCoreBufferPoolContextPin(bufferPoolContext: bufferPoolContext);
    });
}