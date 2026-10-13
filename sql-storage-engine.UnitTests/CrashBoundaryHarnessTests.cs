using sql_storage_engine.Exposures.Diagnostics;
using AwesomeAssertions;
using sql_storage_engine.Diagnostics;

namespace sql_storage_engine.UnitTests;
public sealed class CrashBoundaryHarnessTests
{
    [Test]
    public async Task EveryBoundaryTerminatesAndRecoveryProducesAtomicStateWithValidStructures()
    {
        var durable = false;
        var integrityChecks = 0;
        async Task Execute(Action<CrashBoundary> reach)
        {
            durable = false;
            reach(CrashBoundary.WalAppend);
            reach(CrashBoundary.PageMutation);
            reach(CrashBoundary.Allocation);
            reach(CrashBoundary.CatalogRootUpdate);
            reach(CrashBoundary.WalFlush);
            durable = true;
            reach(CrashBoundary.PageWrite);
            reach(CrashBoundary.Backup);
            await Task.CompletedTask;
        }

        Task<State> Recover() => Task.FromResult(durable ? new State(true, true, true, true) : new State(false, true, true, true));
        var results = await CrashBoundaryHarnessExposure.RunEveryBoundaryAsync(Execute, Recover, ordinal => ordinal >= 5, (state, committed) =>
        {
            state.Committed.Should().Be(committed);
            return Task.CompletedTask;
        }, state =>
        {
            state.HeapValid.Should().BeTrue();
            state.IndexValid.Should().BeTrue();
            state.OverflowValid.Should().BeTrue();
            integrityChecks++;
            return Task.CompletedTask;
        });
        results.Select(result => result.Boundary).Should().Equal(CrashBoundary.WalAppend, CrashBoundary.PageMutation, CrashBoundary.Allocation, CrashBoundary.CatalogRootUpdate, CrashBoundary.WalFlush, CrashBoundary.PageWrite, CrashBoundary.Backup);
        results.Should().HaveCount(7).And.AllSatisfy(result => result.Ordinal.Should().BeInRange(0, 6));
        integrityChecks.Should().Be(7);
    }

    private sealed record State(bool Committed, bool HeapValid, bool IndexValid, bool OverflowValid);
}