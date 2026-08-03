using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Transactions;

/// <summary>
/// Grants transaction-owned logical locks with FIFO ordering per resource. Conversions join the same queue as
/// acquisitions and retain the owner's existing mode while waiting.
/// </summary>
public sealed class LockManager : ILockManager
{
    private readonly object _sync = new();
    private readonly Dictionary<LockResource, ResourceState> _resources = [];
    private readonly Dictionary<TransactionId, HashSet<LockResource>> _owned = [];
    private readonly Dictionary<TransactionId, Action> _victimHandlers = [];
    private long _deadlockCount;
    private TransactionId? _lastDeadlockVictim;

    /// <summary>Gets the number of cycles resolved by this manager.</summary>
    public long DeadlockCount { get { lock (_sync) return _deadlockCount; } }

    /// <summary>Gets the most recently selected deadlock victim, or null before the first cycle.</summary>
    public TransactionId? LastDeadlockVictim { get { lock (_sync) return _lastDeadlockVictim; } }

    /// <summary>Registers the synchronous rollback and resource-release callback used if a transaction is selected as a victim.</summary>
    public void RegisterVictimHandler(TransactionId transactionId, Action handler)
    {
        if (transactionId.Value == 0) throw new ArgumentOutOfRangeException(nameof(transactionId));
        ArgumentNullException.ThrowIfNull(handler);
        lock (_sync)
        {
            if (!_victimHandlers.TryAdd(transactionId, handler))
                throw new InvalidOperationException($"Transaction {transactionId} already has a deadlock-victim handler.");
        }
    }

    public ValueTask AcquireAsync(TransactionId transactionId, LockResource resource, LockMode mode,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(transactionId, resource, mode);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var state = GetOrAdd(resource);
            if (state.Granted.TryGetValue(transactionId, out var current))
            {
                if (current == mode) return ValueTask.CompletedTask;
                throw new InvalidOperationException($"Transaction {transactionId} already owns a {current} lock; use conversion.");
            }
            var request = new LockRequest(transactionId, mode, false);
            state.Waiting.Enqueue(request);
            RegisterCancellation(resource, request, cancellationToken);
            ProcessQueue(resource, state);
            ResolveDeadlocks();
            return new ValueTask(request.Completion.Task);
        }
    }

    public ValueTask ConvertAsync(TransactionId transactionId, LockResource resource, LockMode mode,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(transactionId, resource, mode);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (!_resources.TryGetValue(resource, out var state) ||
                !state.Granted.TryGetValue(transactionId, out var current))
                throw new InvalidOperationException($"Transaction {transactionId} does not own the resource.");
            LockRules.EnsureValidConversion(current, mode);
            if (current == mode) return ValueTask.CompletedTask;
            if (state.Waiting.Any(request => request.TransactionId == transactionId))
                throw new InvalidOperationException($"Transaction {transactionId} already has a waiting request for the resource.");
            var request = new LockRequest(transactionId, mode, true);
            state.Waiting.Enqueue(request);
            RegisterCancellation(resource, request, cancellationToken);
            ProcessQueue(resource, state);
            ResolveDeadlocks();
            return new ValueTask(request.Completion.Task);
        }
    }

    public bool Release(TransactionId transactionId, LockResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        lock (_sync)
        {
            if (!_resources.TryGetValue(resource, out var state) || !state.Granted.Remove(transactionId)) return false;
            RemoveOwnership(transactionId, resource);
            CancelWaitingRequests(state, transactionId);
            ProcessQueue(resource, state);
            RemoveResourceIfEmpty(resource, state);
            return true;
        }
    }

    public void ReleaseAll(TransactionId transactionId)
    {
        lock (_sync)
        {
            foreach (var pair in _resources.ToArray())
            {
                pair.Value.Granted.Remove(transactionId);
                CancelWaitingRequests(pair.Value, transactionId);
                ProcessQueue(pair.Key, pair.Value);
                RemoveResourceIfEmpty(pair.Key, pair.Value);
            }
            _owned.Remove(transactionId);
            _victimHandlers.Remove(transactionId);
        }
    }

    private void ResolveDeadlocks()
    {
        while (FindCycle() is { Count: > 0 } cycle)
        {
            var victim = cycle.MaxBy(transactionId => transactionId.Value);
            _deadlockCount++;
            _lastDeadlockVictim = victim;
            _victimHandlers.TryGetValue(victim, out var handler);
            try { handler?.Invoke(); }
            finally { AbortVictim(victim); }
        }
    }

    private HashSet<TransactionId>? FindCycle()
    {
        var graph = BuildWaitForGraph();
        var visited = new HashSet<TransactionId>();
        var active = new HashSet<TransactionId>();
        var path = new List<TransactionId>();
        foreach (var start in graph.Keys.OrderBy(transactionId => transactionId.Value))
            if (FindCycleFrom(start, graph, visited, active, path) is { } cycle) return cycle;
        return null;
    }

    private static HashSet<TransactionId>? FindCycleFrom(TransactionId current,
        IReadOnlyDictionary<TransactionId, HashSet<TransactionId>> graph, HashSet<TransactionId> visited,
        HashSet<TransactionId> active, List<TransactionId> path)
    {
        if (active.Contains(current))
        {
            var start = path.IndexOf(current);
            return path.Skip(start).ToHashSet();
        }
        if (!visited.Add(current)) return null;
        active.Add(current);
        path.Add(current);
        if (graph.TryGetValue(current, out var dependencies))
            foreach (var dependency in dependencies.OrderBy(transactionId => transactionId.Value))
                if (FindCycleFrom(dependency, graph, visited, active, path) is { } cycle) return cycle;
        path.RemoveAt(path.Count - 1);
        active.Remove(current);
        return null;
    }

    private Dictionary<TransactionId, HashSet<TransactionId>> BuildWaitForGraph()
    {
        var graph = new Dictionary<TransactionId, HashSet<TransactionId>>();
        foreach (var state in _resources.Values)
        {
            var earlierWaiters = new List<TransactionId>();
            foreach (var request in state.Waiting)
            {
                if (!graph.TryGetValue(request.TransactionId, out var dependencies))
                {
                    dependencies = [];
                    graph.Add(request.TransactionId, dependencies);
                }
                foreach (var granted in state.Granted)
                    if (granted.Key != request.TransactionId && !LockRules.AreCompatible(granted.Value, request.Mode))
                        dependencies.Add(granted.Key);
                foreach (var earlier in earlierWaiters)
                    if (earlier != request.TransactionId) dependencies.Add(earlier);
                earlierWaiters.Add(request.TransactionId);
            }
        }
        return graph;
    }

    private void AbortVictim(TransactionId victim)
    {
        foreach (var pair in _resources.ToArray())
        {
            pair.Value.Granted.Remove(victim);
            foreach (var request in pair.Value.Waiting.Where(request => request.TransactionId == victim).ToArray())
                if (RemoveWaiter(pair.Value, request))
                {
                    request.CancellationRegistration.Unregister();
                    request.Completion.TrySetException(new DeadlockException(victim));
                }
            ProcessQueue(pair.Key, pair.Value);
            RemoveResourceIfEmpty(pair.Key, pair.Value);
        }
        _owned.Remove(victim);
        _victimHandlers.Remove(victim);
    }

    private ResourceState GetOrAdd(LockResource resource)
    {
        if (_resources.TryGetValue(resource, out var state)) return state;
        state = new ResourceState();
        _resources.Add(resource, state);
        return state;
    }

    private void ProcessQueue(LockResource resource, ResourceState state)
    {
        while (state.Waiting.TryPeek(out var request) && IsGrantable(state, request))
        {
            state.Waiting.Dequeue();
            request.CancellationRegistration.Unregister();
            state.Granted[request.TransactionId] = request.Mode;
            if (!_owned.TryGetValue(request.TransactionId, out var resources))
            {
                resources = [];
                _owned.Add(request.TransactionId, resources);
            }
            resources.Add(resource);
            request.Completion.TrySetResult();
        }
    }

    private static bool IsGrantable(ResourceState state, LockRequest request) => state.Granted.All(pair =>
        pair.Key == request.TransactionId || LockRules.AreCompatible(pair.Value, request.Mode));

    private void RegisterCancellation(LockResource resource, LockRequest request, CancellationToken token)
    {
        if (!token.CanBeCanceled) return;
        request.CancellationRegistration = token.Register(() => CancelRequest(resource, request, token));
    }

    private void CancelRequest(LockResource resource, LockRequest request, CancellationToken token)
    {
        lock (_sync)
        {
            if (!_resources.TryGetValue(resource, out var state) || !RemoveWaiter(state, request)) return;
            request.Completion.TrySetCanceled(token);
            ProcessQueue(resource, state);
            RemoveResourceIfEmpty(resource, state);
        }
    }

    private static bool RemoveWaiter(ResourceState state, LockRequest target)
    {
        var removed = false;
        var retained = new Queue<LockRequest>();
        while (state.Waiting.TryDequeue(out var request))
            if (ReferenceEquals(request, target)) removed = true;
            else retained.Enqueue(request);
        while (retained.TryDequeue(out var request)) state.Waiting.Enqueue(request);
        return removed;
    }

    private static void CancelWaitingRequests(ResourceState state, TransactionId transactionId)
    {
        foreach (var request in state.Waiting.Where(request => request.TransactionId == transactionId).ToArray())
            if (RemoveWaiter(state, request))
            {
                request.CancellationRegistration.Unregister();
                request.Completion.TrySetException(new InvalidOperationException(
                    $"Transaction {transactionId} released its locks while a request was waiting."));
            }
    }

    private void RemoveOwnership(TransactionId transactionId, LockResource resource)
    {
        if (!_owned.TryGetValue(transactionId, out var resources)) return;
        resources.Remove(resource);
        if (resources.Count == 0) _owned.Remove(transactionId);
    }

    private void RemoveResourceIfEmpty(LockResource resource, ResourceState state)
    {
        if (state.Granted.Count == 0 && state.Waiting.Count == 0) _resources.Remove(resource);
    }

    private static void ValidateRequest(TransactionId transactionId, LockResource resource, LockMode mode)
    {
        if (transactionId.Value == 0) throw new ArgumentOutOfRangeException(nameof(transactionId));
        ArgumentNullException.ThrowIfNull(resource);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
    }

    private sealed class ResourceState
    {
        public Dictionary<TransactionId, LockMode> Granted { get; } = [];
        public Queue<LockRequest> Waiting { get; } = [];
    }

    private sealed class LockRequest(TransactionId transactionId, LockMode mode, bool isConversion)
    {
        public TransactionId TransactionId { get; } = transactionId;
        public LockMode Mode { get; } = mode;
        public bool IsConversion { get; } = isConversion;
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationTokenRegistration CancellationRegistration { get; set; }
    }
}

/// <summary>Releases every lock owned by a transaction on commit, rollback, failure, or disposal.</summary>
public sealed class LockingTransaction : IDisposable
{
    private readonly ILockManager _lockManager;
    private readonly Action _releasePins;

    public LockingTransaction(Transaction transaction, ILockManager lockManager, Action? releasePins = null)
    {
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _lockManager = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
        _releasePins = releasePins ?? (() => { });
        if (lockManager is LockManager concrete)
            concrete.RegisterVictimHandler(transaction.Id, AbortForDeadlock);
    }

    public Transaction Transaction { get; }
    public ValueTask AcquireAsync(LockResource resource, LockMode mode, CancellationToken cancellationToken = default)
    {
        Transaction.EnsureActive();
        return _lockManager.AcquireAsync(Transaction.Id, resource, mode, cancellationToken);
    }

    public ValueTask ConvertAsync(LockResource resource, LockMode mode, CancellationToken cancellationToken = default)
    {
        Transaction.EnsureActive();
        return _lockManager.ConvertAsync(Transaction.Id, resource, mode, cancellationToken);
    }

    public void Commit()
    {
        try { Transaction.Commit(); }
        finally { _lockManager.ReleaseAll(Transaction.Id); }
    }

    public void Rollback()
    {
        try { Transaction.Rollback(); }
        finally { _lockManager.ReleaseAll(Transaction.Id); }
    }

    public void Dispose()
    {
        try { Transaction.Dispose(); }
        finally { _lockManager.ReleaseAll(Transaction.Id); }
    }

    private void AbortForDeadlock()
    {
        try
        {
            if (Transaction.State == TransactionState.Active) Transaction.Rollback();
        }
        finally { _releasePins(); }
    }
}

/// <summary>Reports that a deterministic deadlock victim was rolled back so surviving transactions can continue.</summary>
public sealed class DeadlockException : StorageException
{
    public DeadlockException(TransactionId victimTransactionId)
        : base($"Transaction {victimTransactionId} was selected as the deadlock victim.") =>
        VictimTransactionId = victimTransactionId;

    public TransactionId VictimTransactionId { get; }
}
