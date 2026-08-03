using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class HeapPageLayoutTests
{
    [Test]
    public void Initialize_WritesDocumentedGoldenHeaderBytes()
    {
        var page = new byte[PageConstants.DefaultSize];
        HeapPageLayout.Initialize(page, new PageId(9), new PageId(7), new PageId(11));

        Convert.ToHexString(page.AsSpan(32, 32)).Should().Be(
            "010700000000000000010B000000000000000000400000000020000000000000");
        HeapPageLayout.ReadHeader(page, new PageId(9)).Should().Be(
            new HeapPageHeader(new PageId(7), new PageId(11), 0, 64, PageConstants.DefaultSize));
    }

    [Test]
    public void ReadHeader_RejectsOverlappingAndOutOfBoundsRegions()
    {
        var page = ValidPage();
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(HeapPageLayout.RowDataStartOffset), 63);
        ((Action)(() => HeapPageLayout.ReadHeader(page))).Should().Throw<StorageCorruptionException>();

        page = ValidPage();
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(HeapPageLayout.RowDataStartOffset), (uint)page.Length + 1);
        ((Action)(() => HeapPageLayout.ReadHeader(page))).Should().Throw<StorageCorruptionException>();
    }

    [TestCase(HeapSlotState.Unused)]
    [TestCase(HeapSlotState.Deleted)]
    public void SlotStates_UnusedAndDeletedAreDistinctAndCannotReferenceBytes(HeapSlotState state)
    {
        var page = ValidPage();
        BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(HeapPageLayout.SlotCountOffset), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(HeapPageLayout.SlotDirectoryEndOffset), 80);
        BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(HeapPageLayout.HeaderLength), (ushort)state);

        HeapPageLayout.ReadSlot(page, new SlotId(0)).State.Should().Be(state);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(HeapPageLayout.HeaderLength + 4), 100);
        ((Action)(() => HeapPageLayout.ReadHeader(page))).Should().Throw<StorageCorruptionException>();
    }

    [Test]
    public void ReadHeader_ValidatesEveryLiveSlotBeforeRowsAreExposed()
    {
        var page = ValidPage();
        BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(HeapPageLayout.SlotCountOffset), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(HeapPageLayout.SlotDirectoryEndOffset), 80);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(HeapPageLayout.RowDataStartOffset), 8000);
        BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(HeapPageLayout.HeaderLength), (ushort)HeapSlotState.Live);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(HeapPageLayout.HeaderLength + 4), 8190);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(HeapPageLayout.HeaderLength + 8), 3);

        ((Action)(() => HeapPageLayout.ReadHeader(page))).Should().Throw<StorageCorruptionException>();
    }

    private static byte[] ValidPage()
    {
        var page = new byte[PageConstants.DefaultSize];
        HeapPageLayout.Initialize(page, new PageId(1));
        return page;
    }
}
