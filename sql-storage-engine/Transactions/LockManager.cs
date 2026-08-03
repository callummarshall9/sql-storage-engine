using sql_storage_engine.Identifiers;

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
        }
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

    public LockingTransaction(Transaction transaction, ILockManager lockManager)
    {
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _lockManager = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
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
}
