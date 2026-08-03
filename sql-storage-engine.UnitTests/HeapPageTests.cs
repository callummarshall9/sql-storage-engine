using AwesomeAssertions;
using System.Buffers.Binary;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class HeapPageTests
{
    [Test]
    public void InsertAndRead_MultipleRowsRoundTripExactly()
    {
        var heap = CreateHeap(out _);
        var rows = new[] { new byte[] { 1 }, new byte[] { 2, 3, 4 }, Enumerable.Repeat((byte)5, 300).ToArray() };
        var identifiers = new List<(SlotId SlotId, SlotGeneration Generation)>();

        foreach (var row in rows)
        {
            heap.TryInsert(row, out var slotId, out var generation).Should().BeTrue();
            identifiers.Add((slotId, generation));
        }

        for (var index = 0; index < rows.Length; index++)
        {
            heap.TryRead(identifiers[index].SlotId, identifiers[index].Generation, out var actual).Should().BeTrue();
            actual.ToArray().Should().Equal(rows[index]);
        }
    }

    [Test]
    public void Insert_TooLargeLeavesPageByteForByteUnchanged()
    {
        var heap = CreateHeap(out var page);
        heap.TryInsert(new byte[100], out _, out _).Should().BeTrue();
        var before = page.ToArray();

        heap.TryInsert(new byte[heap.FreeBytes], out _, out _).Should().BeFalse();

        page.Should().Equal(before);
    }

    [Test]
    public void Insert_RejectsEmptyAndAcceptsExactlyAvailablePayload()
    {
        var heap = CreateHeap(out _);
        ((Action)(() => heap.TryInsert(ReadOnlySpan<byte>.Empty, out _, out _))).Should().Throw<ArgumentException>();
        var exactPayload = heap.FreeBytes - HeapPageLayout.SlotEntryLength;

        heap.TryInsert(new byte[exactPayload], out var slotId, out var generation).Should().BeTrue();
        heap.FreeBytes.Should().Be(0);
        heap.TryRead(slotId, generation, out var row).Should().BeTrue();
        row.Length.Should().Be(exactPayload);
    }

    [Test]
    public void Read_ReturnsCopyThatCannotMutatePageState()
    {
        var heap = CreateHeap(out _);
        heap.TryInsert(new byte[] { 10, 20 }, out var slotId, out var generation).Should().BeTrue();
        heap.TryRead(slotId, generation, out var first).Should().BeTrue();
        first.ToArray()[0] = 99;

        heap.TryRead(slotId, generation, out var second).Should().BeTrue();
        second.ToArray().Should().Equal(10, 20);
    }

    [Test]
    public void DeleteAndReuse_RejectsStaleIdentifiersAcrossEveryOperation()
    {
        var heap = CreateHeap(out _);
        heap.TryInsert(new byte[] { 1, 2 }, out var oldSlot, out var oldGeneration).Should().BeTrue();
        heap.Delete(oldSlot, oldGeneration).Should().BeTrue();
        heap.Delete(oldSlot, oldGeneration).Should().BeFalse();
        heap.TryRead(oldSlot, oldGeneration, out _).Should().BeFalse();
        heap.TryInsert(new byte[] { 3, 4 }, out var replacementSlot, out var replacementGeneration).Should().BeTrue();

        replacementSlot.Should().Be(oldSlot);
        replacementGeneration.Should().NotBe(oldGeneration);
        heap.TryRead(oldSlot, oldGeneration, out _).Should().BeFalse();
        heap.Update(oldSlot, oldGeneration, new byte[] { 9, 9 }).Should().Be(HeapUpdateResult.Absent);
        heap.Delete(oldSlot, oldGeneration).Should().BeFalse();
        heap.TryRead(replacementSlot, replacementGeneration, out var replacement).Should().BeTrue();
        replacement.ToArray().Should().Equal(3, 4);
    }

    [Test]
    public void Reuse_MaximumGenerationSlotIsRetiredRatherThanWrapped()
    {
        var heap = CreateHeap(out var page);
        heap.TryInsert(new byte[] { 1 }, out var firstSlot, out _).Should().BeTrue();
        BinaryPrimitives.WriteUInt32LittleEndian(
            page.AsSpan(HeapPageLayout.HeaderLength + 12), uint.MaxValue);

        heap.Delete(firstSlot, new SlotGeneration(uint.MaxValue)).Should().BeTrue();
        heap.TryInsert(new byte[] { 2 }, out var nextSlot, out var nextGeneration).Should().BeTrue();

        nextSlot.Should().Be(new SlotId(1));
        nextGeneration.Should().Be(default(SlotGeneration));
        heap.TryRead(firstSlot, new SlotGeneration(uint.MaxValue), out _).Should().BeFalse();
    }

    [Test]
    public void Compact_PreservesLiveRowsAndIdentifiersAndIsIdempotent()
    {
        var heap = CreateHeap(out var page);
        heap.TryInsert(new byte[] { 1, 1 }, out var firstSlot, out var firstGeneration).Should().BeTrue();
        heap.TryInsert(new byte[] { 2, 2, 2 }, out var deletedSlot, out var deletedGeneration).Should().BeTrue();
        heap.TryInsert(new byte[] { 3, 3, 3, 3 }, out var thirdSlot, out var thirdGeneration).Should().BeTrue();
        var freeBeforeDelete = heap.FreeBytes;
        heap.Delete(deletedSlot, deletedGeneration).Should().BeTrue();

        heap.Compact();
        var once = page.ToArray();

        heap.FreeBytes.Should().Be(freeBeforeDelete + 3);
        heap.TryRead(firstSlot, firstGeneration, out var first).Should().BeTrue();
        heap.TryRead(thirdSlot, thirdGeneration, out var third).Should().BeTrue();
        first.ToArray().Should().Equal(1, 1);
        third.ToArray().Should().Equal(3, 3, 3, 3);
        HeapPageLayout.ReadSlot(page, deletedSlot).State.Should().Be(HeapSlotState.Deleted);
        heap.Compact();
        page.Should().Equal(once);
    }

    [Test]
    public void RandomInsertDeleteCompact_AgreesWithReferenceModel()
    {
        const int seed = 8675309;
        var random = new Random(seed);
        var heap = CreateHeap(out var page);
        var live = new Dictionary<(SlotId Slot, SlotGeneration Generation), byte[]>();

        for (var operation = 0; operation < 500; operation++)
        {
            if (live.Count != 0 && random.Next(3) == 0)
            {
                var selected = live.ElementAt(random.Next(live.Count));
                heap.Delete(selected.Key.Slot, selected.Key.Generation).Should().BeTrue($"seed {seed}, operation {operation}");
                live.Remove(selected.Key);
            }
            else
            {
                var bytes = new byte[random.Next(1, 96)];
                random.NextBytes(bytes);
                if (!heap.TryInsert(bytes, out var slot, out var generation))
                {
                    heap.Compact();
                    if (!heap.TryInsert(bytes, out slot, out generation)) continue;
                }
                live.Add((slot, generation), bytes);
            }

            if (random.Next(4) == 0)
            {
                heap.Compact();
                _ = HeapPageLayout.ReadHeader(page);
                foreach (var expected in live)
                {
                    heap.TryRead(expected.Key.Slot, expected.Key.Generation, out var actual)
                        .Should().BeTrue($"seed {seed}, operation {operation}");
                    actual.ToArray().Should().Equal(expected.Value, $"seed {seed}, operation {operation}");
                }
            }
        }
    }

    [Test]
    public void Update_SameSmallerAndLargerFittingRowsMaintainCorrectBytesAndFreeSpace()
    {
        var heap = CreateHeap(out _);
        heap.TryInsert(new byte[] { 1, 2, 3, 4 }, out var slot, out var generation).Should().BeTrue();
        var initialFree = heap.FreeBytes;

        heap.Update(slot, generation, new byte[] { 4, 3, 2, 1 }).Should().Be(HeapUpdateResult.Updated);
        heap.FreeBytes.Should().Be(initialFree);
        AssertRow(heap, slot, generation, 4, 3, 2, 1);

        heap.Update(slot, generation, new byte[] { 8, 9 }).Should().Be(HeapUpdateResult.Updated);
        heap.FreeBytes.Should().Be(initialFree + 2);
        AssertRow(heap, slot, generation, 8, 9);

        heap.Update(slot, generation, new byte[] { 5, 6, 7, 8, 9, 10 }).Should().Be(HeapUpdateResult.Updated);
        heap.FreeBytes.Should().Be(initialFree - 2);
        AssertRow(heap, slot, generation, 5, 6, 7, 8, 9, 10);
    }

    [Test]
    public void Update_NonFittingAndStaleRowsLeaveOriginalPageUnchanged()
    {
        var heap = CreateHeap(out var page);
        heap.TryInsert(new byte[] { 1, 2, 3 }, out var slot, out var generation).Should().BeTrue();
        var beforeRelocation = page.ToArray();

        heap.Update(slot, generation, new byte[page.Length]).Should().Be(HeapUpdateResult.RelocationRequired);
        page.Should().Equal(beforeRelocation);
        AssertRow(heap, slot, generation, 1, 2, 3);

        var beforeStale = page.ToArray();
        heap.Update(slot, new SlotGeneration(generation.Value + 1), new byte[] { 9, 9, 9 })
            .Should().Be(HeapUpdateResult.Absent);
        page.Should().Equal(beforeStale);
    }

    private static void AssertRow(HeapPage heap, SlotId slot, SlotGeneration generation, params byte[] expected)
    {
        heap.TryRead(slot, generation, out var actual).Should().BeTrue();
        actual.ToArray().Should().Equal(expected);
    }

    internal static HeapPage CreateHeap(out byte[] page, PageId? pageId = null)
    {
        page = new byte[PageConstants.DefaultSize];
        HeapPageLayout.Initialize(page, pageId ?? new PageId(1));
        return new HeapPage(page, pageId ?? new PageId(1));
    }
}
