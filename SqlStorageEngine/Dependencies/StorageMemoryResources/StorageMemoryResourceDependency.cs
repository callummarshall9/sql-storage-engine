// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.StorageMemoryResources;
internal sealed class StorageMemoryResourceDependency : IDisposable
{
    private readonly Dictionary<Guid, (SemaphoreSlim Gate, int References, bool Closed)> gates = new Dictionary<Guid, (SemaphoreSlim Gate, int References, bool Closed)>();

    private readonly object sync = new object();

    public Guid Create()
    {
        var handle = Guid.NewGuid();

        lock (sync)
        {
            gates.Add(key: handle, value: (new SemaphoreSlim(initialCount: 1, maxCount: 1), 0, false));
        }

        return handle;
    }

    public async ValueTask EnterAsync(Guid handle, CancellationToken cancellationToken)
    {
        SemaphoreSlim gate;

        lock (sync)
        {
            var state = gates[handle];

            ObjectDisposedException.ThrowIf(condition: state.Closed, instance: this);

            gates[handle] = (state.Gate, checked(state.References + 1), false);

            gate = state.Gate;
        }

        try
        {
            await gate.WaitAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch
        {
            lock (sync)
            {
                var state = gates[handle];

                state.References--;

                if (state.Closed && state.References == 0)
                {
                    gates.Remove(key: handle);

                    state.Gate.Dispose();
                }
                else
                {
                    gates[handle] = state;
                }
            }

            throw;
        }
    }

    public void Exit(Guid handle)
    {
        lock (sync)
        {
            var state = gates[handle];

            state.Gate.Release();

            state.References--;

            if (state.Closed && state.References == 0)
            {
                gates.Remove(key: handle);

                state.Gate.Dispose();
            }
            else
            {
                gates[handle] = state;
            }
        }
    }

    public void Close(Guid handle)
    {
        lock (sync)
        {
            var state = gates[handle];

            state.Closed = true;

            if (state.References == 0)
            {
                gates.Remove(key: handle);

                state.Gate.Dispose();
            }
            else
            {
                gates[handle] = state;
            }
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            foreach (var state in gates.Values)
            {
                state.Gate.Dispose();
            }

            gates.Clear();
        }
    }

}