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
            .Should().Be("xVv+/J+SamhOV4gEUC2DyyPk+sFp5DK011DIeaAhZMI=");
    }

    [Test]
    public void Version7CatalogWithoutTemporalMetadataRemainsReadable()
    {
        var version8 = CatalogCodec.Encode(Sample());
        BinaryPrimitives.WriteUInt32LittleEndian(version8, 0x38544143);
        BinaryPrimitives.WriteUInt16LittleEndian(version8.AsSpan(4), 8);
        const int indexRecordLength = 58;
        var temporalMarkerOffset = version8.Length - indexRecordLength - 1;
        var version7 = version8[..temporalMarkerOffset].Concat(version8[(temporalMarkerOffset + 1)..]).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(version7, 0x37544143);
        BinaryPrimitives.WriteUInt16LittleEndian(version7.AsSpan(4), 7);

        var decoded = CatalogCodec.Decode(version7);

        decoded.Tables.Single().SystemVersioning.Should().BeNull();
        decoded.Tables.Single().QualifiedName.Should().Be(Sample().Tables.Single().QualifiedName);
    }

    [Test]
    public void Version8SpatialIndexesRemainReadableWithAllSridCompatibility()
    {
        var table = new CatalogTable(new TableId(1), "places", 1, new PageId(2), [
            new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
            new CatalogColumn(new ColumnId(2), "point", SqlType.Geometry, true)
        ]);
        var catalog = new CatalogDefinition([table], [
            new CatalogIndex(new IndexId(1), "pk", table.Id, new PageId(3), true,
                [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)],
                storageKind: CatalogIndexStorageKind.Clustered, isPrimaryKey: true),
            new CatalogIndex(new IndexId(2), "spatial", table.Id, new PageId(4), false,
                [new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.First)],
                CatalogSpecializedIndexOptions.Spatial())
        ]);
        var version9 = CatalogCodec.Encode(catalog);
        var spatialSridMarkerOffset = version9.Length - 10;
        var version8 = version9[..spatialSridMarkerOffset]
            .Concat(version9[(spatialSridMarkerOffset + 1)..]).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(version8, 0x38544143);
        BinaryPrimitives.WriteUInt16LittleEndian(version8.AsSpan(4), 8);

        var decoded = CatalogCodec.Decode(version8);

        decoded.Indexes.Single(index => index.Method == CatalogIndexMethod.Spatial)
            .SpecializedOptions!.SpatialSrid.Should().BeNull();
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
