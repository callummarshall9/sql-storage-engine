using AwesomeAssertions;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Exposures.Diagnostics;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.UnitTests;
public sealed class CrashInjectionExposureTests
{
    [Test]
    public void InvalidBoundaryDoesNotAdvanceTheTerminationOrdinal()
    {
        var injection = new CrashInjectionExposure(terminateAtOrdinal: 0);
        ((Action)(() => injection.Reach((CrashBoundary)0))).Should().Throw<StorageModelValidationException>().Which.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        injection.GetBoundaries().Should().BeEmpty();
        var termination = ((Action)(() => injection.Reach(CrashBoundary.WalFlush))).Should().Throw<SimulatedProcessTerminationException>().Which;
        termination.Ordinal.Should().Be(0);
        termination.Boundary.Should().Be(CrashBoundary.WalFlush);
        termination.InnerException.Should().BeOfType<SimulatedProcessTerminationException>();
        injection.GetBoundaries().Should().Equal(CrashBoundary.WalFlush);
    }

    [Test]
    public void BoundarySnapshotsAreReadOnlyAndDoNotChangeWhenFurtherPointsAreRecorded()
    {
        var injection = new CrashInjectionExposure();
        injection.Reach(CrashBoundary.PageMutation);
        var snapshot = injection.GetBoundaries();
        injection.Reach(CrashBoundary.PageWrite);
        snapshot.Should().Equal(CrashBoundary.PageMutation);
        ((Action)(() => ((IList<CrashBoundary>)snapshot).Clear())).Should().Throw<NotSupportedException>();
        injection.GetBoundaries().Should().Equal(CrashBoundary.PageMutation, CrashBoundary.PageWrite);
    }

    [Test]
    public void NegativeTerminationOrdinalIsRejectedBeforeRecording()
    {
        var injection = new CrashInjectionExposure(terminateAtOrdinal: -1);
        ((Action)(() => injection.Reach(CrashBoundary.Backup))).Should().Throw<StorageModelValidationException>().Which.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        injection.GetBoundaries().Should().BeEmpty();
    }
}