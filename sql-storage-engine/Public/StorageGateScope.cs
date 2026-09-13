namespace sql_storage_engine;

internal sealed class StorageGateScope
{
    private readonly object _sync = new();
    private readonly List<IAsyncDisposable> _resources = [];
    private bool _active = true;
    private TaskCompletionSource? _child;

    internal void EnterChild()
    {
        lock (_sync)
        {
            if (!_active) throw new InvalidOperationException("Storage access scope has ended.");
            if (_child is not null) throw new InvalidOperationException("Concurrent storage operations inside a callback are not supported.");
            _child = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    internal void ExitChild()
    {
        lock (_sync)
        {
            _child?.TrySetResult();
            _child = null;
        }
    }

    internal void Register(IAsyncDisposable resource)
    {
        lock (_sync)
        {
            if (!_active) throw new InvalidOperationException("The statement scope has completed.");
            _resources.Add(resource);
        }
    }

    internal async Task End()
    {
        IAsyncDisposable[] resources;
        Task child;
        lock (_sync)
        {
            _active = false;
            resources = _resources.ToArray();
            _resources.Clear();
            child = _child?.Task ?? Task.CompletedTask;
        }
        foreach (var resource in resources) await resource.DisposeAsync().ConfigureAwait(false);
        await child.ConfigureAwait(false);
    }
}
