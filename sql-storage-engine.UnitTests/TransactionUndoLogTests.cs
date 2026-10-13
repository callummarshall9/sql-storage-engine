using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Exposures.Transactions;
using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
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
        var log = new TransactionUndoExposure();
        var page = new byte[]
        {
            9,
            9,
            9
        };
        log.RecordBeforeImage(kind,page,new byte[] { 1, 2, 3 });
        page.AsSpan().Fill(7);
        log.Rollback();
        page.Should().Equal(1, 2, 3);
        log.IsRecoveryRequired().Should().BeFalse();
    }

    [Test]
    public void UndoAndAllocatedPageReclamation_RunInStrictReverseMutationOrder()
    {
        var order = new List<string>();
        var log = new TransactionUndoExposure();
        log.RecordUndo(TransactionMutationKind.HeapInsert,() => order.Add("first"));
        log.RecordUndo(TransactionMutationKind.IndexSplit,() => order.Add("second"));
        log.TrackAllocatedPageId(new sql_storage_engine.Models.Identifiers.PageId { Value = 1 },() => order.Add("page1"));
        log.TrackAllocatedPageId(new sql_storage_engine.Models.Identifiers.PageId { Value = 2 },() => order.Add("page2"));
        log.Rollback();
        order.Should().Equal("second", "first", "page2", "page1");
    }

    [Test]
    public void UndoFailure_MarksDatabaseAsRequiringRecovery()
    {
        var log = new TransactionUndoExposure();
        log.RecordUndo(TransactionMutationKind.HeapUpdate, () => throw new IOException("undo failed"));
        ((Action)log.Rollback).Should().Throw<StorageModelDependencyException>().Which.InnerException.Should().BeOfType<IOException>();
        log.IsRecoveryRequired().Should().BeTrue();
    }

    [Test]
    public void AllocatedPagesAreReclaimedOnRollbackWhileRetiredPagesAreNotReused()
    {
        var reclaimed = false;
        var log = new TransactionUndoExposure();
        log.TrackAllocatedPageId(new sql_storage_engine.Models.Identifiers.PageId { Value = 4 },() => reclaimed = true);
        log.TrackRetiredPageId(new sql_storage_engine.Models.Identifiers.PageId { Value = 5 });
        log.Rollback();
        reclaimed.Should().BeTrue();
        log.GetRetiredPageIds().Should().BeEmpty();
    }
}