using AwesomeAssertions;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Exposures.Diagnostics;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.UnitTests;
public sealed class CrashBoundaryHarnessExposureTests
{
    [TestCase(0, "execute")]
    [TestCase(1, "reopenAndRecover")]
    [TestCase(2, "expectedCommitted")]
    [TestCase(3, "verifyState")]
    [TestCase(4, "verifyIntegrity")]
    public async Task MissingCallbackIsRejectedBeforeExecutingAWorkload(int missingIndex, string parameterName)
    {
        var executions = 0;
        Func<Action<CrashBoundary>, Task>? execute = missingIndex == 0 ? null : _ =>
        {
            executions++;
            return Task.CompletedTask;
        };
        Func<Task<int>>? reopen = missingIndex == 1 ? null : () => Task.FromResult(0);
        Func<int, bool>? expected = missingIndex == 2 ? null : _ => false;
        Func<int, bool, Task>? verify = missingIndex == 3 ? null : (_, _) => Task.CompletedTask;
        Func<int, Task>? integrity = missingIndex == 4 ? null : _ => Task.CompletedTask;
        var failure = await ((Func<Task>)(async () => await CrashBoundaryHarnessExposure.RunEveryBoundaryAsync(execute!, reopen!, expected!, verify!, integrity!))).Should().ThrowAsync<StorageModelValidationException>();
        failure.Which.InnerException.Should().BeOfType<ArgumentNullException>().Which.ParamName.Should().Be(parameterName);
        executions.Should().Be(0);
    }

    [Test]
    public async Task MissingReplayTerminationIsClassifiedBeforeRecoveryOrVerification()
    {
        var executions = 0;
        var reopenings = 0;
        Task Execute(Action<CrashBoundary> reach)
        {
            if (executions++ == 0)
                reach(CrashBoundary.WalFlush);
            return Task.CompletedTask;
        }

        Task<int> Reopen()
        {
            reopenings++;
            return Task.FromResult(0);
        }

        var failure = await ((Func<Task>)(async () => await CrashBoundaryHarnessExposure.RunEveryBoundaryAsync(Execute, Reopen, _ => false, (_, _) => Task.CompletedTask, _ => Task.CompletedTask))).Should().ThrowAsync<CrashRunValidationException>();
        failure.Which.InnerException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Contain("did not terminate");
        executions.Should().Be(2);
        reopenings.Should().Be(0);
    }

    [Test]
    public async Task CancellationDuringRecoveryPreservesItsTokenAndStopsVerification()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var verifications = 0;
        Task Execute(Action<CrashBoundary> reach)
        {
            reach(CrashBoundary.PageWrite);
            return Task.CompletedTask;
        }

        var failure = await ((Func<Task>)(async () => await CrashBoundaryHarnessExposure.RunEveryBoundaryAsync(Execute, () => Task.FromCanceled<int>(cancellation.Token), _ => false, (_, _) =>
        {
            verifications++;
            return Task.CompletedTask;
        }, _ => Task.CompletedTask))).Should().ThrowAsync<OperationCanceledException>();
        failure.Which.CancellationToken.Should().Be(cancellation.Token);
        failure.Which.InnerException.Should().BeAssignableTo<OperationCanceledException>();
        verifications.Should().Be(0);
    }
}