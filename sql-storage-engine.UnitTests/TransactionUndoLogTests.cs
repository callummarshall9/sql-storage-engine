using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class TransactionUndoLogTests
{
    [TestCase(TransactionMutationKind.HeapInsert)]
    [TestCase(TransactionMutationKind.HeapUpdate)]
    [TestCase(TransactionMutationKind.HeapDelete)]
    [TestCase(TransactionMutationKind.IndexSplit)]
    [TestCase(TransactionMutationKind.OverflowReplacement)]
    [TestCase(TransactionMutationKind.CatalogChange)]
    public void EveryStorageMutation_RestoresItsBeforeImage(TransactionMutationKind kind)
    {
        var recovery = new RecoveryRequirement();
        var log = new TransactionUndoLog(recovery);
        var page = new byte[] { 9, 9, 9 };
        log.RecordBeforeImage(kind, page, new byte[] { 1, 2, 3 });
        page.AsSpan().Fill(7);
        log.Rollback();
        page.Should().Equal(1, 2, 3);
        recovery.RecoveryRequired.Should().BeFalse();
    }

    [Test]
    public void UndoAndAllocatedPageReclamation_RunInStrictReverseMutationOrder()
    {
        var order = new List<string>();
        var log = new TransactionUndoLog(new RecoveryRequirement());
        log.RecordUndo(TransactionMutationKind.HeapInsert, () => order.Add("first"));
        log.RecordUndo(TransactionMutationKind.IndexSplit, () => order.Add("second"));
        log.TrackAllocatedPage(new PageId(1), () => order.Add("page1"));
        log.TrackAllocatedPage(new PageId(2), () => order.Add("page2"));
        log.Rollback();
        order.Should().Equal("second", "first", "page2", "page1");
    }

    [Test]
    public void UndoFailure_MarksDatabaseAsRequiringRecovery()
    {
        var recovery = new RecoveryRequirement();
        var log = new TransactionUndoLog(recovery);
        log.RecordUndo(TransactionMutationKind.HeapUpdate, () => throw new IOException("undo failed"));
        ((Action)log.Rollback).Should().Throw<IOException>();
        recovery.RecoveryRequired.Should().BeTrue();
    }

    [Test]
    public void AllocatedPagesAreReclaimedOnRollbackWhileRetiredPagesAreNotReused()
    {
        var reclaimed = false;
        var log = new TransactionUndoLog(new RecoveryRequirement());
        log.TrackAllocatedPage(new PageId(4), () => reclaimed = true);
        log.TrackRetiredPage(new PageId(5));
        log.Rollback();
        reclaimed.Should().BeTrue();
        log.RetiredPages.Should().BeEmpty();
    }
}
