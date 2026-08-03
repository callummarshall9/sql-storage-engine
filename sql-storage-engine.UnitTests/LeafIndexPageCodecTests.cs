using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class LeafIndexPageCodecTests
{
    [Test]
    public void LeafEntriesLinksAndDuplicates_RoundTrip()
    {
        var model = new LeafIndexPage(new PageId(5), new PageId(2), new PageId(3), new PageId(8), new[]
        {
            Entry(1, 10, 1, 2), Entry(1, 11, 2, 3), Entry(9, 12, 3, 4)
        });
        var page = new byte[PageConstants.DefaultSize];
        LeafIndexPageCodec.Write(page, model);

        var actual = LeafIndexPageCodec.Read(page, model.PageId);

        actual.ParentPageId.Should().Be(model.ParentPageId);
        actual.PreviousPageId.Should().Be(model.PreviousPageId);
        actual.NextPageId.Should().Be(model.NextPageId);
        actual.Entries.Should().Equal(model.Entries);
    }

    [Test]
    public void EmptyLeaf_RoundTripsAndMalformedOffsetsAndRowIdsAreRejected()
    {
        var page = new byte[PageConstants.DefaultSize];
        var empty = new LeafIndexPage(new PageId(1), null, null, null, Array.Empty<LeafIndexEntry>());
        LeafIndexPageCodec.Write(page, empty);
        LeafIndexPageCodec.Read(page, empty.PageId).Entries.Should().BeEmpty();

        var model = new LeafIndexPage(new PageId(5), null, null, null, new[] { Entry(1, 2, 0, 0) });
        LeafIndexPageCodec.Write(page, model);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(LeafIndexPageCodec.HeaderLength), uint.MaxValue);
        PageChecksum.WriteChecksum(page, page.Length);
        ((Action)(() => LeafIndexPageCodec.Read(page, model.PageId))).Should().Throw<StorageCorruptionException>();

        LeafIndexPageCodec.Write(page, model);
        BinaryPrimitives.WriteUInt64LittleEndian(page.AsSpan(LeafIndexPageCodec.HeaderLength + 8), 0);
        PageChecksum.WriteChecksum(page, page.Length);
        ((Action)(() => LeafIndexPageCodec.Read(page, model.PageId))).Should().Throw<StorageCorruptionException>();
    }

    [Test]
    public void UnorderedEntriesAreRejectedBeforeEncoding()
    {
        var page = new byte[PageConstants.DefaultSize];
        var model = new LeafIndexPage(new PageId(5), null, null, null,
            new[] { Entry(2, 2, 0, 0), Entry(1, 3, 0, 0) });
        ((Action)(() => LeafIndexPageCodec.Write(page, model))).Should().Throw<ArgumentException>();
    }

    private static LeafIndexEntry Entry(byte key, ulong page, ushort slot, uint generation) =>
        new(new IndexKey(new[] { key }), new RowId(new PageId(page), new SlotId(slot), new SlotGeneration(generation)));
}
