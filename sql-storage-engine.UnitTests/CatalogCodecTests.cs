using System.Buffers.Binary;
using System.Security.Cryptography;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class CatalogCodecTests
{
    private static CatalogDefinition Sample() => new(
        [new CatalogTable(new TableId(1), "t", 2, new PageId(3),
            [new CatalogColumn(new ColumnId(4), "c", SqlType.VarChar(100, "Latin1_General_100_BIN2"), true)])],
        [new CatalogIndex(new IndexId(5), "i", new TableId(1), new PageId(6), true,
            [new CatalogIndexedColumn(new ColumnId(4), SortDirection.Descending, NullSortOrder.First, "o")])]);

    [Test]
    public void TableAndIndexDefinitions_RoundTripThroughBootstrapFormat()
    {
        var decoded = CatalogCodec.Decode(CatalogCodec.Encode(Sample()));
        decoded.Tables.Single().Should().BeEquivalentTo(Sample().Tables.Single());
        decoded.Indexes.Single().Should().BeEquivalentTo(Sample().Indexes.Single());
    }

    [Test]
    public void SampleCatalog_ProducesCommittedGoldenBytes()
    {
        Convert.ToBase64String(SHA256.HashData(CatalogCodec.Encode(Sample())))
            .Should().Be("gOz2+uZYX479Mc6+adxqge+zikwnG3/wbPBtQDRZWno=");
    }

    [Test]
    public void UnknownVersionAndEveryTruncation_AreRejected()
    {
        var encoded = CatalogCodec.Encode(Sample());
        BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(4), 99);
        ((Func<CatalogDefinition>)(() => CatalogCodec.Decode(encoded))).Should().Throw<StorageFormatException>();
        encoded = CatalogCodec.Encode(Sample());
        for (var length = 0; length < encoded.Length; length++)
            ((Func<CatalogDefinition>)(() => CatalogCodec.Decode(encoded.AsSpan(0, length))))
                .Should().Throw<StorageFormatException>($"length {length} is truncated");
    }

    [Test]
    public void InvalidPersistedCrossReference_IsReportedAsCorruption()
    {
        var encoded = CatalogCodec.Encode(Sample());
        // The sample has no records after its 58-byte index record. Its table ID starts 13 bytes into that record.
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(encoded.Length - 45), 99);
        ((Func<CatalogDefinition>)(() => CatalogCodec.Decode(encoded))).Should().Throw<StorageCorruptionException>();
    }
}
