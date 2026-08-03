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
        async Task Execute(CrashInjector crash)
        {
            durable = false;
            crash.Reach(CrashBoundary.WalAppend);
            crash.Reach(CrashBoundary.PageMutation);
            crash.Reach(CrashBoundary.Allocation);
            crash.Reach(CrashBoundary.CatalogRootUpdate);
            crash.Reach(CrashBoundary.WalFlush);
            durable = true;
            crash.Reach(CrashBoundary.PageWrite);
            crash.Reach(CrashBoundary.Backup);
            await Task.CompletedTask;
        }
        Task<State> Recover() => Task.FromResult(durable
            ? new State(true, true, true, true) : new State(false, true, true, true));

        var results = await CrashBoundaryHarness.RunEveryBoundaryAsync(Execute, Recover,
            ordinal => ordinal >= 5,
            (state, committed) => { state.Committed.Should().Be(committed); return Task.CompletedTask; },
            state =>
            {
                state.HeapValid.Should().BeTrue(); state.IndexValid.Should().BeTrue(); state.OverflowValid.Should().BeTrue();
                integrityChecks++; return Task.CompletedTask;
            });

        results.Select(result => result.Boundary).Should().Equal(CrashBoundary.WalAppend,
            CrashBoundary.PageMutation, CrashBoundary.Allocation, CrashBoundary.CatalogRootUpdate,
            CrashBoundary.WalFlush, CrashBoundary.PageWrite, CrashBoundary.Backup);
        results.Should().HaveCount(7).And.AllSatisfy(result => result.Ordinal.Should().BeInRange(0, 6));
        integrityChecks.Should().Be(7);
    }

    private sealed record State(bool Committed, bool HeapValid, bool IndexValid, bool OverflowValid);
}
