// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;
using sql_storage_engine.Services.Processings.LockRules;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Transactions;
using sql_storage_engine.Services.Processings.LockResources;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Orchestrations.LockManagers;
internal sealed partial class LockManagerContextOrchestrationService : ILockManagerContextOrchestrationService
{
    private readonly IIndexKeyLockResourceProcessingService lockRelations;
    private readonly object _sync = new();
    private readonly ILockRulesProcessingService _lockRules;
    private readonly Dictionary<LockResource, LockResourceState> _resources = [];
    private readonly Dictionary<TransactionId, Action> _victimHandlers = [];
    private long _deadlockCount;
    private long _nextRequestSequence;
    private TransactionId? _lastDeadlockVictim;
    private long ReadDeadlockCountCore()
    {
        lock (_sync)
        {
            return _deadlockCount;
        }
    }

    private TransactionId? ReadLastDeadlockVictimCore()
    {
        lock (_sync)
        {
            return _lastDeadlockVictim;
        }
    }

    private void RegisterVictimHandlerCore(TransactionId transactionId, Action handler)
    {
        lock (_sync)
        {
            if (!_victimHandlers.TryAdd(key: transactionId, value: handler))
            {
                throw new InvalidOperationException($"Transaction {transactionId} already has a deadlock-victim handler.");
            }
        }
    }

    private ValueTask AcquireAsyncCore(TransactionId transactionId, LockResource resource, LockMode mode, CancellationToken cancellationToken = default)
    {
        resource = lockRelations.PrepareLockResource(lockResource: resource);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var state = GetOrAdd(resource: resource);

            if (state.Granted.TryGetValue(key: transactionId, value: out var current))
            {
                if (current == mode)
                {
                    return ValueTask.CompletedTask;
                }

                throw new InvalidOperationException($"Transaction {transactionId} already owns a {current} lock; use conversion.");
            }

            var request = new LockRequest
            {
                TransactionId = transactionId,
                Mode = mode,
                IsConversion = false,
                Sequence = ++_nextRequestSequence,
                Completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };

            state.Waiting.Enqueue(item: request);
            RegisterCancellation(resource: resource, request: request, token: cancellationToken);
            ProcessQueues();
            ResolveDeadlocks();
            return new ValueTask(request.Completion.Task);
        }
    }

    private ValueTask ConvertAsyncCore(TransactionId transactionId, LockResource resource, LockMode mode, CancellationToken cancellationToken = default)
    {
        resource = lockRelations.PrepareLockResource(lockResource: resource);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (!_resources.TryGetValue(key: resource, value: out var state) || !state.Granted.TryGetValue(key: transactionId, value: out var current))
            {
                throw new InvalidOperationException($"Transaction {transactionId} does not own the resource.");
            }

            _lockRules.EnsureValidConversion(current: current, requested: mode);

            if (current == mode)
            {
                return ValueTask.CompletedTask;
            }

            if (state.Waiting.Any(predicate: request => request.TransactionId == transactionId))
            {
                throw new InvalidOperationException($"Transaction {transactionId} already has a waiting request for the resource.");
            }

            var request = new LockRequest
            {
                TransactionId = transactionId,
                Mode = mode,
                IsConversion = true,
                Sequence = ++_nextRequestSequence,
                Completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
            };

            state.Waiting.Enqueue(item: request);
            RegisterCancellation(resource: resource, request: request, token: cancellationToken);
            ProcessQueues();
            ResolveDeadlocks();
            return new ValueTask(request.Completion.Task);
        }
    }

    private bool ReleaseCore(TransactionId transactionId, LockResource resource)
    {
        resource = lockRelations.PrepareLockResource(lockResource: resource);

        lock (_sync)
        {
            if (!_resources.TryGetValue(key: resource, value: out var state) || !state.Granted.Remove(key: transactionId))
            {
                return false;
            }

            CancelWaitingRequests(state: state, transactionId: transactionId);
            ProcessQueues();
            RemoveResourceIfEmpty(resource: resource, state: state);
            return true;
        }
    }

    private void ReleaseAllCore(TransactionId transactionId)
    {
        lock (_sync)
        {
            foreach (var pair in _resources.ToArray())
            {
                pair.Value.Granted.Remove(key: transactionId);
                CancelWaitingRequests(state: pair.Value, transactionId: transactionId);
                ProcessQueues();
                RemoveResourceIfEmpty(resource: pair.Key, state: pair.Value);
            }

            _victimHandlers.Remove(key: transactionId);
        }
    }

    private void ResolveDeadlocks()
    {
        while (FindCycle()is { Count: > 0 } cycle)
        {
            var victim = cycle.MaxBy(keySelector: transactionId => transactionId.Value);
            _deadlockCount++;
            _lastDeadlockVictim = victim;
            _victimHandlers.TryGetValue(key: victim, value: out var handler);

            try
            {
                handler?.Invoke();
            }
            finally
            {
                AbortVictim(victim: victim);
            }
        }
    }

    private HashSet<TransactionId>? FindCycle()
    {
        var graph = BuildWaitForGraph();
        var visited = new HashSet<TransactionId>();
        var active = new HashSet<TransactionId>();
        var path = new List<TransactionId>();

        foreach (var start in graph.Keys.OrderBy(keySelector: transactionId => transactionId.Value))
        {
            if (FindCycleFrom(current: start, graph: graph, visited: visited, active: active, path: path)is { } cycle)
            {
                return cycle;
            }
        }

        return null;
    }

    private static HashSet<TransactionId>? FindCycleFrom(TransactionId current, IReadOnlyDictionary<TransactionId, HashSet<TransactionId>> graph, HashSet<TransactionId> visited, HashSet<TransactionId> active, List<TransactionId> path)
    {
        if (active.Contains(item: current))
        {
            var start = path.IndexOf(item: current);

            return path.Skip(count: start)
            .ToHashSet();
        }

        if (!visited.Add(item: current))
        {
            return null;
        }

        active.Add(item: current);
        path.Add(item: current);

        if (graph.TryGetValue(key: current, value: out var dependencies))
        {
            foreach (var dependency in dependencies.OrderBy(keySelector: transactionId => transactionId.Value))
            {
                if (FindCycleFrom(current: dependency, graph: graph, visited: visited, active: active, path: path)is { } cycle)
                {
                    return cycle;
                }
            }
        }

        path.RemoveAt(index: path.Count - 1);
        active.Remove(item: current);
        return null;
    }

    private Dictionary<TransactionId, HashSet<TransactionId>> BuildWaitForGraph()
    {
        var graph = new Dictionary<TransactionId, HashSet<TransactionId>>();

        var waiters = _resources.SelectMany(selector: pair => pair.Value.Waiting.Select(selector: request => (Resource: pair.Key, Request: request)))
            .OrderBy(keySelector: item => item.Request.Sequence)
            .ToArray();

        foreach (var waiter in waiters)
        {
            var request = waiter.Request;

            if (!graph.TryGetValue(key: request.TransactionId, value: out var dependencies))
            {
                dependencies = [];
                graph.Add(key: request.TransactionId, value: dependencies);
            }

            foreach (var pair in _resources)
            {
                if (lockRelations.CheckLockResourceConflict(lockResources: [waiter.Resource, pair.Key]))
                {
                    foreach (var granted in pair.Value.Granted)
                    {
                        if (granted.Key != request.TransactionId && !_lockRules.AreCompatible(first: granted.Value, second: request.Mode))
                        {
                            dependencies.Add(item: granted.Key);
                        }
                    }
                }
            }

            foreach (var earlier in waiters.Where(predicate: item => item.Request.Sequence < request.Sequence && lockRelations.CheckLockResourceConflict(lockResources: [waiter.Resource, item.Resource])))
            {
                if (earlier.Request.TransactionId != request.TransactionId)
                {
                    dependencies.Add(item: earlier.Request.TransactionId);
                }
            }
        }

        return graph;
    }

    private void AbortVictim(TransactionId victim)
    {
        foreach (var pair in _resources.ToArray())
        {
            pair.Value.Granted.Remove(key: victim);

            foreach (var request in pair.Value.Waiting.Where(predicate: request => request.TransactionId == victim)
            .ToArray())
            {
                if (RemoveWaiter(state: pair.Value, target: request))
                {
                    request.CancellationRegistration.Unregister();
                    request.Completion.TrySetException(exception: new DeadlockException(victim));
                }
            }

            ProcessQueues();
            RemoveResourceIfEmpty(resource: pair.Key, state: pair.Value);
        }

        _victimHandlers.Remove(key: victim);
    }

    private LockResourceState GetOrAdd(LockResource resource)
    {
        if (_resources.TryGetValue(key: resource, value: out var state))
        {
            return state;
        }

        state = new LockResourceState
        {
            Granted = [],
            Waiting = []
        };

        _resources.Add(key: resource, value: state);
        return state;
    }

    private void ProcessQueues()
    {
        bool granted;

        do
        {
            granted = false;

            foreach (var pair in _resources.ToArray())
            {
                var resource = pair.Key;
                var state = pair.Value;

                if (!state.Waiting.TryPeek(result: out var request) || !IsGrantable(resource: resource, request: request))
                {
                    continue;
                }

                state.Waiting.Dequeue();
                request.CancellationRegistration.Unregister();
                state.Granted[request.TransactionId] = request.Mode;

                request.Completion.TrySetResult();
                granted = true;
            }
        }
        while (granted);
    }

    private bool IsGrantable(LockResource resource, LockRequest request)
    {
        foreach (var pair in _resources)
        {
            if (!lockRelations.CheckLockResourceConflict(lockResources: [resource, pair.Key]))
            {
                continue;
            }

            if (pair.Value.Granted.Any(predicate: granted => granted.Key != request.TransactionId && !_lockRules.AreCompatible(first: granted.Value, second: request.Mode)))
            {
                return false;
            }

            if (pair.Value.Waiting.Any(predicate: waiter => waiter.Sequence < request.Sequence && waiter.TransactionId != request.TransactionId))
            {
                return false;
            }
        }

        return true;
    }

    private void RegisterCancellation(LockResource resource, LockRequest request, CancellationToken token)
    {
        if (!token.CanBeCanceled)
        {
            return;
        }

        request.CancellationRegistration = token.Register(callback: () => CancelRequest(resource: resource, request: request, token: token));
    }

    private void CancelRequest(LockResource resource, LockRequest request, CancellationToken token)
    {
        lock (_sync)
        {
            if (!_resources.TryGetValue(key: resource, value: out var state) || !RemoveWaiter(state: state, target: request))
            {
                return;
            }

            request.Completion.TrySetCanceled(cancellationToken: token);
            ProcessQueues();
            RemoveResourceIfEmpty(resource: resource, state: state);
        }
    }

    private static bool RemoveWaiter(LockResourceState state, LockRequest target)
    {
        var removed = false;
        var retained = new Queue<LockRequest>();

        while (state.Waiting.TryDequeue(result: out var request))
        {
            if (ReferenceEquals(objA: request, objB: target))
            {
                removed = true;
            }
            else
            {
                retained.Enqueue(item: request);
            }
        }

        while (retained.TryDequeue(result: out var request))
        {
            state.Waiting.Enqueue(item: request);
        }

        return removed;
    }

    private static void CancelWaitingRequests(LockResourceState state, TransactionId transactionId)
    {
        foreach (var request in state.Waiting.Where(predicate: request => request.TransactionId == transactionId)
            .ToArray())
        {
            if (RemoveWaiter(state: state, target: request))
            {
                request.CancellationRegistration.Unregister();
                request.Completion.TrySetException(exception: new InvalidOperationException($"Transaction {transactionId} released its locks while a request was waiting."));
            }
        }
    }

    private void RemoveResourceIfEmpty(LockResource resource, LockResourceState state)
    {
        if (state.Granted.Count == 0 && state.Waiting.Count == 0)
        {
            _resources.Remove(key: resource);
        }
    }

    public LockManagerContextOrchestrationService(IIndexKeyLockResourceProcessingService lockRelations, ILockRulesProcessingService lockRules)
    {
        this.lockRelations = lockRelations;
        _lockRules = lockRules;
    }

    public void RegisterLockManagerContextVictimHandler(LockManagerContext lockManagerContext) =>
        TryCatch(operation: () =>
    {
        ValidateVictimHandler(lockManagerContext: lockManagerContext);
        RegisterVictimHandlerCore(transactionId: lockManagerContext.TransactionId, handler: lockManagerContext.VictimHandler!);
    });

    public ValueTask AcquireLockManagerContextAsync(LockManagerContext lockManagerContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateAcquisition(lockManagerContext: lockManagerContext, cancellationToken: cancellationToken);
        return AcquireAsyncCore(transactionId: lockManagerContext.TransactionId, resource: lockManagerContext.LockResource!, mode: lockManagerContext.Mode, cancellationToken: cancellationToken);
    });

    public ValueTask ConvertLockManagerContextAsync(LockManagerContext lockManagerContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateAcquisition(lockManagerContext: lockManagerContext, cancellationToken: cancellationToken);
        return ConvertAsyncCore(transactionId: lockManagerContext.TransactionId, resource: lockManagerContext.LockResource!, mode: lockManagerContext.Mode, cancellationToken: cancellationToken);
    });

    public bool ReleaseLockManagerContext(LockManagerContext lockManagerContext) =>
        TryCatch(operation: () =>
    {
        ValidateRelease(lockManagerContext: lockManagerContext);
        return ReleaseCore(transactionId: lockManagerContext.TransactionId, resource: lockManagerContext.LockResource!);
    });

    public void ReleaseAllLockManagerContext(LockManagerContext lockManagerContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(lockManagerContext: lockManagerContext);
        ReleaseAllCore(transactionId: lockManagerContext.TransactionId);
    });

    public long ReadLockManagerContextDeadlockCount(LockManagerContext lockManagerContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(lockManagerContext: lockManagerContext);
        return ReadDeadlockCountCore();
    });

    public LockManagerContext ReadLockManagerContextLastVictim(LockManagerContext lockManagerContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(lockManagerContext: lockManagerContext);

        return lockManagerContext with
        {
            LastVictim = ReadLastDeadlockVictimCore()
        };
    });
}