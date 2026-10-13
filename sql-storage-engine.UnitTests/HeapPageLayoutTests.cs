// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Heap;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Heap;

namespace sql_storage_engine.UnitTests;
public sealed class HeapPageLayoutTests
{
    [Test]
    public void Initialize_WritesDocumentedGoldenHeaderBytes()
    {
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        sql_storage_engine.Exposures.Heap.HeapPageLayoutExposure.InitializeHeapPageLayout(page,(new sql_storage_engine.Models.Identifiers.PageId { Value = 9 }).Value,(new sql_storage_engine.Models.Identifiers.PageId { Value = 7 }).Value,(new sql_storage_engine.Models.Identifiers.PageId { Value = 11 }).Value);
        Convert.ToHexString(page.AsSpan(32, 32)).Should().Be("010700000000000000010B000000000000000000400000000020000000000000");
        sql_storage_engine.Exposures.Heap.HeapPageLayoutExposure.ReadHeapPageHeader(page,(new sql_storage_engine.Models.Identifiers.PageId { Value = 9 }).Value).Should().Be(new HeapPageHeader { PreviousPageId = new sql_storage_engine.Models.Identifiers.PageId { Value = 7 }, NextPageId = new sql_storage_engine.Models.Identifiers.PageId { Value = 11 }, SlotCount = 0, SlotDirectoryEnd = 64, RowDataStart = sql_storage_engine.Models.Pages.PageConstants.DefaultSize });
    }

    [Test]
    public void ReadHeader_RejectsOverlappingAndOutOfBoundsRegions()
    {
        var page = ValidPage();
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(sql_storage_engine.Models.Heap.HeapPageFormat.RowDataStartOffset), 63);
        ((Action)(() => sql_storage_engine.Exposures.Heap.HeapPageLayoutExposure.ReadHeapPageHeader(page))).Should().Throw<StorageCorruptionException>();
        page = ValidPage();
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(sql_storage_engine.Models.Heap.HeapPageFormat.RowDataStartOffset), (uint)page.Length + 1);
        ((Action)(() => sql_storage_engine.Exposures.Heap.HeapPageLayoutExposure.ReadHeapPageHeader(page))).Should().Throw<StorageCorruptionException>();
    }

    [TestCase(HeapSlotState.Unused)]
    [TestCase(HeapSlotState.Deleted)]
    public void SlotStates_UnusedAndDeletedAreDistinctAndCannotReferenceBytes(HeapSlotState state)
    {
        var page = ValidPage();
        BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(sql_storage_engine.Models.Heap.HeapPageFormat.SlotCountOffset), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(sql_storage_engine.Models.Heap.HeapPageFormat.SlotDirectoryEndOffset), 80);
        BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(sql_storage_engine.Models.Heap.HeapPageFormat.HeaderLength), (ushort)state);
        sql_storage_engine.Exposures.Heap.HeapPageLayoutExposure.ReadHeapSlotEntry(page,(new sql_storage_engine.Models.Identifiers.SlotId { Value = 0 }).Value).State.Should().Be(state);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(sql_storage_engine.Models.Heap.HeapPageFormat.HeaderLength + 4), 100);
        ((Action)(() => sql_storage_engine.Exposures.Heap.HeapPageLayoutExposure.ReadHeapPageHeader(page))).Should().Throw<StorageCorruptionException>();
    }

    [Test]
    public void ReadHeader_ValidatesEveryLiveSlotBeforeRowsAreExposed()
    {
        var page = ValidPage();
        BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(sql_storage_engine.Models.Heap.HeapPageFormat.SlotCountOffset), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(sql_storage_engine.Models.Heap.HeapPageFormat.SlotDirectoryEndOffset), 80);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(sql_storage_engine.Models.Heap.HeapPageFormat.RowDataStartOffset), 8000);
        BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(sql_storage_engine.Models.Heap.HeapPageFormat.HeaderLength), (ushort)HeapSlotState.Live);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(sql_storage_engine.Models.Heap.HeapPageFormat.HeaderLength + 4), 8190);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(sql_storage_engine.Models.Heap.HeapPageFormat.HeaderLength + 8), 3);
        ((Action)(() => sql_storage_engine.Exposures.Heap.HeapPageLayoutExposure.ReadHeapPageHeader(page))).Should().Throw<StorageCorruptionException>();
    }

    private static byte[] ValidPage()
    {
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        sql_storage_engine.Exposures.Heap.HeapPageLayoutExposure.InitializeHeapPageLayout(page,(new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }).Value);
        return page;
    }
}