using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Heap;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Heap;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class HeapPageArchitectureTests
{
    [Test]
    public void BoundPageDetectsChangedOwnershipBeforeAnOperation()
    {
        var bytes = new byte[PageConstants.DefaultSize];
        HeapPageLayoutExposure.InitializeHeapPageLayout(bytes,1);
        var page = sql_storage_engine.Exposures.Heap.HeapPageExposure.BindHeapPage(bytes,new PageId { Value = 1 });
        BinaryPrimitives.WriteUInt64LittleEndian(bytes,2);
        ((Action)(() => page.GetPageId())).Should().Throw<StorageCorruptionException>();
        ((Action)(() => page.Compact())).Should().Throw<StorageCorruptionException>();
    }

    [Test]
    public void ResultProjectionPreservesAbsenceStalenessAndIndependentRowCopies()
    {
        var bytes = new byte[PageConstants.DefaultSize];
        HeapPageLayoutExposure.InitializeHeapPageLayout(bytes,1);
        var page = sql_storage_engine.Exposures.Heap.HeapPageExposure.BindHeapPage(bytes);
        page.TryInsert(new byte[] { 1, 2, 3 },out var slotId,out var generation).Should().BeTrue();
        page.Read(slotId,generation,out var row).Should().Be(HeapReadResult.Found);
        row.ToArray().Should().Equal(1, 2, 3);
        var copy = row.ToArray();
        copy[0] = 99;
        page.TryRead(slotId,generation,out var second).Should().BeTrue();
        second.ToArray().Should().Equal(1, 2, 3);
        page.Read(slotId,new SlotGeneration { Value = generation.Value + 1 },out var stale).Should().Be(HeapReadResult.StaleGeneration);
        stale.IsEmpty.Should().BeTrue();
        page.Read(new SlotId { Value = ushort.MaxValue },generation,out var absent).Should().Be(HeapReadResult.UnknownSlot);
        absent.IsEmpty.Should().BeTrue();
        page.Delete(slotId,generation).Should().BeTrue();
        page.Read(slotId,generation,out var deleted).Should().Be(HeapReadResult.Deleted);
        deleted.IsEmpty.Should().BeTrue();
    }
}