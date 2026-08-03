namespace sql_storage_engine.Diagnostics;

public enum CrashBoundary
{
    WalAppend = 1, WalFlush = 2, PageMutation = 3, PageWrite = 4,
    Allocation = 5, CatalogRootUpdate = 6, Backup = 7
}
public sealed class SimulatedProcessTerminationException(CrashBoundary boundary, int ordinal)
    : Exception($"Simulated process termination at {boundary} boundary {ordinal}.")
{
    public CrashBoundary Boundary { get; } = boundary;
    public int Ordinal { get; } = ordinal;
}

/// <summary>Records or terminates execution at deterministic durable-write boundaries.</summary>
public sealed class CrashInjector(int? terminateAtOrdinal = null)
{
    private int _ordinal;
    private readonly List<CrashBoundary> _boundaries = [];
    public IReadOnlyList<CrashBoundary> Boundaries => _boundaries;
    public void Reach(CrashBoundary boundary)
    {
        if (!Enum.IsDefined(boundary)) throw new ArgumentOutOfRangeException(nameof(boundary));
        _boundaries.Add(boundary);
        var ordinal = _ordinal++;
        if (terminateAtOrdinal == ordinal) throw new SimulatedProcessTerminationException(boundary, ordinal);
    }
}

public sealed record CrashRunResult(int Ordinal, CrashBoundary Boundary, bool ExpectedCommitted);

/// <summary>Restarts and verifies state and structural integrity after every instrumented crash point.</summary>
public static class CrashBoundaryHarness
{
    public static async Task<IReadOnlyList<CrashRunResult>> RunEveryBoundaryAsync<TState>(
        Func<CrashInjector, Task> execute, Func<Task<TState>> reopenAndRecover,
        Func<int, bool> expectedCommitted, Func<TState, bool, Task> verifyState,
        Func<TState, Task> verifyIntegrity)
    {
        var discovery = new CrashInjector();
        await execute(discovery).ConfigureAwait(false);
        var results = new List<CrashRunResult>();
        for (var ordinal = 0; ordinal < discovery.Boundaries.Count; ordinal++)
        {
            var injector = new CrashInjector(ordinal);
            try { await execute(injector).ConfigureAwait(false); throw new InvalidOperationException("Boundary did not terminate execution."); }
            catch (SimulatedProcessTerminationException exception) when (exception.Ordinal == ordinal) { }
            var state = await reopenAndRecover().ConfigureAwait(false);
            var committed = expectedCommitted(ordinal);
            await verifyState(state, committed).ConfigureAwait(false);
            await verifyIntegrity(state).ConfigureAwait(false);
            results.Add(new CrashRunResult(ordinal, discovery.Boundaries[ordinal], committed));
        }
        return results.AsReadOnly();
    }
}
