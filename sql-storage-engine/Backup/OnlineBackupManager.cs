using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Backup;

/// <summary>Retains WAL from the oldest active backup start until each registration is disposed.</summary>
public sealed class WalRetentionRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<long, LogSequenceNumber> _registrations = [];
    private long _nextId;

    public int ActiveCount { get { lock (_sync) return _registrations.Count; } }
    public LogSequenceNumber? MinimumRetainedLsn
    {
        get { lock (_sync) return _registrations.Count == 0 ? null : _registrations.Values.MinBy(lsn => lsn.Value); }
    }

    public IDisposable Register(LogSequenceNumber startLsn)
    {
        if (startLsn.Value == 0) throw new ArgumentOutOfRangeException(nameof(startLsn));
        lock (_sync)
        {
            var id = ++_nextId;
            _registrations.Add(id, startLsn);
            return new Registration(this, id);
        }
    }

    private void Release(long id) { lock (_sync) _registrations.Remove(id); }
    private sealed class Registration(WalRetentionRegistry owner, long id) : IDisposable
    {
        private WalRetentionRegistry? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release(id);
    }
}

/// <summary>Creates a physical backup while writes continue and retains every WAL position needed by its copy.</summary>
public sealed class OnlineBackupManager(OfflineBackupManager physicalBackup, WalRetentionRegistry retention,
    Func<LogSequenceNumber> currentDurableLsn)
{
    public async Task<BackupManifest> CreateAsync(string databasePath, IEnumerable<string> walPaths,
        string destinationDirectory, Func<CancellationToken, ValueTask>? afterStart = null,
        CancellationToken cancellationToken = default)
    {
        var startLsn = currentDurableLsn();
        using var registration = retention.Register(startLsn);
        if (afterStart is not null) await afterStart(cancellationToken).ConfigureAwait(false);
        var endLsn = currentDurableLsn();
        if (endLsn.Value < startLsn.Value)
            throw new InvalidOperationException("The durable WAL position moved backwards during backup.");
        return await physicalBackup.CreatePhysicalAsync(databasePath, walPaths, destinationDirectory, false,
            startLsn, endLsn, cancellationToken).ConfigureAwait(false);
    }
}
