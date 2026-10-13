using AwesomeAssertions;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;
public sealed class TransactionUndoExposureTests
{
    [Test]
    public void CommitKeepsRetirementEvidenceAndDiscardsUndoWithoutReclaimingAllocations()
    {
        var undoCalls = 0;
        var reclaimCalls = 0;
        var log = new TransactionUndoExposure();
        var allocated = new PageId
        {
            Value = 1
        };
        var retired = new PageId
        {
            Value = 2
        };
        log.RecordUndo(TransactionMutationKind.HeapDelete,() => undoCalls++);
        log.TrackAllocatedPageId(allocated,() => reclaimCalls++);
        log.TrackRetiredPageId(retired);
        log.Commit();
        undoCalls.Should().Be(0);
        reclaimCalls.Should().Be(0);
        log.GetAllocatedPageIds().Should().BeEmpty();
        log.GetRetiredPageIds().Should().Equal(retired);
        log.IsRecoveryRequired().Should().BeFalse();
        ((Action)log.Commit).Should().Throw<TransactionUndoValidationException>();
        ((Action)log.Rollback).Should().Throw<TransactionUndoValidationException>();
    }

    [Test]
    public void BeforeImageIsCopiedAtRecordingTime()
    {
        var log = new TransactionUndoExposure();
        var original = new byte[]
        {
            1,
            2,
            3
        };
        var destination = new byte[]
        {
            9,
            9,
            9
        };
        log.RecordBeforeImage(TransactionMutationKind.HeapUpdate,destination,original);
        original.AsSpan().Fill(7);
        destination.AsSpan().Fill(8);
        log.Rollback();
        destination.Should().Equal(1, 2, 3);
        original.Should().Equal(7, 7, 7);
    }

    [Test]
    public void InvalidInputsDoNotAppendActionsOrModifyTheDestination()
    {
        var log = new TransactionUndoExposure();
        var calls = 0;
        var destination = new byte[]
        {
            9,
            9,
            9
        };
        ((Action)(() => log.RecordUndo((TransactionMutationKind)(-1),() => calls++))).Should().Throw<StorageModelValidationException>();
        ((Action)(() => log.RecordUndo(TransactionMutationKind.HeapInsert,null!))).Should().Throw<StorageModelValidationException>().Which.InnerException.Should().BeOfType<ArgumentNullException>().Which.ParamName.Should().Be("undo");
        ((Action)(() => log.RecordBeforeImage(TransactionMutationKind.HeapUpdate,destination,new byte[] { 1 }))).Should().Throw<StorageModelValidationException>().Which.ParamName.Should().Be("beforeImage");
        log.Rollback();
        calls.Should().Be(0);
        destination.Should().Equal(9, 9, 9);
        log.IsRecoveryRequired().Should().BeFalse();
    }

    [Test]
    public void HeaderPageCannotBeTrackedForAllocationReclamationOrRetirement()
    {
        var log = new TransactionUndoExposure();
        ((Action)(() => log.TrackAllocatedPageId(default,() =>
        {
        }))).Should().Throw<StorageModelValidationException>();
        ((Action)(() => log.TrackRetiredPageId(default))).Should().Throw<StorageModelValidationException>();
        log.GetAllocatedPageIds().Should().BeEmpty();
        log.GetRetiredPageIds().Should().BeEmpty();
    }

    [Test]
    public void ReclamationFailureStopsCleanupAndRequiresRecoveryWithoutReplayingUndo()
    {
        var log = new TransactionUndoExposure();
        var order = new List<string>();
        log.RecordUndo(TransactionMutationKind.HeapInsert,() => order.Add("undo"));
        log.TrackAllocatedPageId(new PageId { Value = 1 },() => order.Add("reclaim1"));
        var failure = new IOException("reclaim failed");
        log.TrackAllocatedPageId(new PageId { Value = 2 },() =>
        {
            order.Add("reclaim2");
            throw failure;
        });
        var retired = new PageId
        {
            Value = 3
        };
        log.TrackRetiredPageId(retired);
        ((Action)log.Rollback).Should().Throw<StorageModelDependencyException>().Which.InnerException.Should().BeSameAs(failure);
        order.Should().Equal("undo", "reclaim2");
        log.IsRecoveryRequired().Should().BeTrue();
        log.GetRetiredPageIds().Should().Equal(retired);
        log.GetAllocatedPageIds().Select(page => page.Value).Should().Equal(1, 2);
        ((Action)log.Rollback).Should().Throw<TransactionUndoValidationException>();
        ((Action)log.Commit).Should().Throw<TransactionUndoValidationException>();
        ((Action)(() => log.RecordUndo(TransactionMutationKind.HeapDelete,() => order.Add("late")))).Should().Throw<TransactionUndoValidationException>();
        order.Should().Equal("undo", "reclaim2");
    }

    [Test]
    public void PageEvidenceSnapshotsRemainReadOnlyAndIndependentOfLaterChanges()
    {
        var log = new TransactionUndoExposure();
        var first = new PageId
        {
            Value = 1
        };
        log.TrackAllocatedPageId(first,() =>
        {
        });
        log.TrackRetiredPageId(first);
        var allocated = log.GetAllocatedPageIds();
        var retired = log.GetRetiredPageIds();
        log.TrackAllocatedPageId(new PageId { Value = 2 },() =>
        {
        });
        log.Commit();
        allocated.Should().Equal(first);
        retired.Should().Equal(first);
        ((Action)(() => ((IList<PageId>)allocated).Clear())).Should().Throw<NotSupportedException>();
        ((Action)(() => ((IList<PageId>)retired).Clear())).Should().Throw<NotSupportedException>();
    }
}