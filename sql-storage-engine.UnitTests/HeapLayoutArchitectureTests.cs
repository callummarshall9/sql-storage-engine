using AwesomeAssertions;
using sql_storage_engine.Exposures.Heap;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Heap;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class HeapLayoutArchitectureTests
{
    [Test]
    public void SelectedSlotReadStillValidatesEverySiblingSlot()
    {
        var page = BuildPage();
        HeapPageLayoutExposure.ReadHeapSlotEntry(page,0).Length.Should().Be(6);
        var siblingOffset = HeapPageLayoutExposure.CalculateHeapSlotEntryOffset(1);
        page[siblingOffset + 2] = 1;
        ((Action)(() => HeapPageLayoutExposure.ReadHeapSlotEntry(page,0)))
            .Should().Throw<StorageFormatException>();
    }

    [Test]
    public void InvalidHeaderAndSlotWritesLeaveThePageUnchanged()
    {
        var page = BuildPage();
        var original = page.ToArray();
        var invalidHeader = new HeapPageHeader { SlotCount = 2, SlotDirectoryEnd = 64, RowDataStart = 4084 };
        ((Action)(() => HeapPageLayoutExposure.WriteHeapPageHeader(page,invalidHeader)))
            .Should().Throw<StorageModelValidationException>();
        page.Should().Equal(original);
        var invalidSlot = new HeapSlotEntry { State = HeapSlotState.Live, Offset = 4095, Length = 2 };
        ((Action)(() => HeapPageLayoutExposure.WriteHeapSlotEntry(page,0,invalidSlot)))
            .Should().Throw<StorageModelValidationException>();
        page.Should().Equal(original);
    }

    [Test]
    public void InitializationRejectsSelfLinksBeforeClearingTheDestination()
    {
        var page = Enumerable.Repeat((byte)0xCC, PageConstants.MinimumSize).ToArray();
        ((Action)(() => HeapPageLayoutExposure.InitializeHeapPageLayout(page, 1, previousPageId: 1)))
            .Should().Throw<StorageModelValidationException>();
        page.Should().OnlyContain(value => value == 0xCC);
    }

    [Test]
    public void SlotOffsetsAreBoundedByTheLargestSupportedPage()
    {
        var maximumSlot = (ushort)((PageConstants.MaximumSize - HeapPageFormat.HeaderLength) / HeapPageFormat.SlotEntryLength - 1);
        HeapPageLayoutExposure.CalculateHeapSlotEntryOffset(maximumSlot).Should().Be(PageConstants.MaximumSize - HeapPageFormat.SlotEntryLength);
        ((Action)(() => HeapPageLayoutExposure.CalculateHeapSlotEntryOffset((ushort)(maximumSlot + 1))))
            .Should().Throw<StorageModelValidationException>();
    }

    private static byte[] BuildPage()
    {
        var page = new byte[PageConstants.MinimumSize];
        HeapPageLayoutExposure.InitializeHeapPageLayout(page,1);
        HeapPageLayoutExposure.WriteHeapSlotEntry(page,0,            new HeapSlotEntry { State = HeapSlotState.Live, Offset = 4084, Length = 6, Generation = new SlotGeneration { Value = 1 } });
        HeapPageLayoutExposure.WriteHeapSlotEntry(page,1,            new HeapSlotEntry { State = HeapSlotState.Live, Offset = 4090, Length = 6, Generation = new SlotGeneration { Value = 1 } });
        HeapPageLayoutExposure.WriteHeapPageHeader(page,            new HeapPageHeader { SlotCount = 2, SlotDirectoryEnd = 96, RowDataStart = 4084 });
        return page;
    }
}