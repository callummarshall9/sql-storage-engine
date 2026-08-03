using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class DatabaseHeaderTests
{
    [Test]
    public void Codec_RoundTripsDatabaseHeaderWithoutMutatingInputOnRead()
    {
        var expected = Header();
        var page = new byte[expected.PageSize];
        DatabaseHeaderCodec.Write(page, expected);
        var snapshot = page.ToArray();
        DatabaseHeaderCodec.Read(page).Should().Be(expected);
        page.Should().Equal(snapshot);
        Convert.ToHexString(page.AsSpan(32, 24)).Should().Be("53514C53544F524500112233445566778899AABBCCDDEEFF");
    }

    [Test]
    public void Codec_ReportsMagicChecksumPageSizeAndVersionSeparately()
    {
        var page = new byte[PageConstants.DefaultSize];
        DatabaseHeaderCodec.Write(page, Header());
        page[32] ^= 1;
        ((Action)(() => DatabaseHeaderCodec.Read(page))).Should().Throw<InvalidDatabaseMagicException>();
        DatabaseHeaderCodec.Write(page, Header()); page[^1] = 1;
        ((Action)(() => DatabaseHeaderCodec.Read(page))).Should().Throw<StorageCorruptionException>();
        DatabaseHeaderCodec.Write(page, Header()); page[60] = 1;
        ((Action)(() => DatabaseHeaderCodec.Read(page))).Should().Throw<InvalidPageSizeException>();
        DatabaseHeaderCodec.Write(page, Header()); page[56] = 2;
        ((Action)(() => DatabaseHeaderCodec.Read(page))).Should().Throw<UnsupportedDatabaseVersionException>();
    }

    private static DatabaseHeader Header() => new(
        new DatabaseId(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")), PageConstants.DefaultSize,
        DatabaseHeader.CurrentFormatVersion, new PageId(1), null, new TableId(2), new IndexId(3),
        new TransactionId(4), new PageId(5), true);
}
