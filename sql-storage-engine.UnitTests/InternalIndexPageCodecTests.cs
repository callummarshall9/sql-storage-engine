using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class InternalIndexPageCodecTests
{
    [Test]
    public void MinimumInternalPage_RoundTripsAndHasGoldenHeader()
    {
        var page = new byte[PageConstants.DefaultSize];
        var expected = new InternalIndexPage(new PageId(5), new PageId(2),
            new[] { Key(10) }, new[] { new PageId(3), new PageId(4) });
        InternalIndexPageCodec.Write(page, expected);

        var actual = InternalIndexPageCodec.Read(page, expected.PageId);

        actual.PageId.Should().Be(expected.PageId);
        actual.ParentPageId.Should().Be(expected.ParentPageId);
        actual.Separators.Should().Equal(expected.Separators);
        actual.Children.Should().Equal(expected.Children);
        Convert.ToHexString(page.AsSpan(32, 32)).Should().Be(
            "0102000000000000000100020050000000FF1F00000000000300000000000000");
    }

    [Test]
    public void EmptyAndMalformedInternalPagesAreRejected()
    {
        var page = new byte[PageConstants.DefaultSize];
        ((Action)(() => InternalIndexPageCodec.Write(page,
            new InternalIndexPage(new PageId(1), null, Array.Empty<IndexKey>(), new[] { new PageId(2) }))))
            .Should().Throw<ArgumentException>();
        var model = new InternalIndexPage(new PageId(5), null, new[] { Key(1) }, new[] { new PageId(2), new PageId(3) });
        InternalIndexPageCodec.Write(page, model);
        BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(InternalIndexPageCodec.ChildCountOffset), 1);
        PageChecksum.WriteChecksum(page, page.Length);
        ((Action)(() => InternalIndexPageCodec.Read(page, model.PageId))).Should().Throw<StorageCorruptionException>();
        InternalIndexPageCodec.Write(page, model);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(InternalIndexPageCodec.HeaderLength), uint.MaxValue);
        PageChecksum.WriteChecksum(page, page.Length);
        ((Action)(() => InternalIndexPageCodec.Read(page, model.PageId))).Should().Throw<StorageCorruptionException>();
    }

    [Test]
    public void MaximumOccupancy_RoundTripsAndOneMoreEntryDoesNotFit()
    {
        var separators = Enumerable.Range(0, 451).Select(index => new IndexKey(new[] { (byte)(index / 256), (byte)index })).ToArray();
        var children = Enumerable.Range(1, separators.Length + 1).Select(index => new PageId((ulong)index + 10)).ToArray();
        var page = new byte[PageConstants.DefaultSize];
        InternalIndexPageCodec.Write(page, new InternalIndexPage(new PageId(1), null, separators, children));
        InternalIndexPageCodec.Read(page, new PageId(1)).Separators.Should().HaveCount(separators.Length);
        InternalIndexPageCodec.CanFit(page.Length, separators.Append(new IndexKey(new byte[] { 0xff, 0xff, 0xff }))).Should().BeFalse();
    }

    private static IndexKey Key(byte value) => new(new[] { value });
}
