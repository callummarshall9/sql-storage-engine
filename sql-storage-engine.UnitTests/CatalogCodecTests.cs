using System.Buffers.Binary;
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
            [new CatalogColumn(new ColumnId(4), "c", SqlType.Text, true)])],
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
        Convert.ToHexString(CatalogCodec.Encode(Sample())).Should().Be(
            "4341543101000000010000000100000001000000000000000100740200000000000000030000000000000001000000040000000000000001006303010000050000000000000001006901000000000000000600000000000000010001000400000000000000020101006F");
    }

    [Test]
    public void UnknownVersionAndEveryTruncation_AreRejected()
    {
        var encoded = CatalogCodec.Encode(Sample());
        BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(4), 2);
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
        // The index table ID follows its fixed ID and one-byte name in this golden fixture.
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(73), 99);
        ((Func<CatalogDefinition>)(() => CatalogCodec.Decode(encoded))).Should().Throw<StorageCorruptionException>();
    }
}
