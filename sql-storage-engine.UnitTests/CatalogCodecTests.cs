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
            .Should().Be("uzNbagABC7gBatD2cQTus3jcSAG+AyXcMinXj37OKIY=");
    }

    [Test]
    public void Version7CatalogWithoutTemporalMetadataRemainsReadable()
    {
        var version10 = CatalogCodec.Encode(Sample());
        const int indexRecordLength = 58;
        var graphMarkerOffset = version10.Length - indexRecordLength - 1;
        var version9 = version10[..graphMarkerOffset].Concat(version10[(graphMarkerOffset + 1)..]).ToArray();
        var temporalMarkerOffset = version9.Length - indexRecordLength - 1;
        var version7 = version9[..temporalMarkerOffset].Concat(version9[(temporalMarkerOffset + 1)..]).ToArray();
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
        var version10 = CatalogCodec.Encode(catalog);
        var tableOnlyLength = CatalogCodec.Encode(new CatalogDefinition([table], [])).Length;
        var version9 = version10[..(tableOnlyLength - 1)].Concat(version10[tableOnlyLength..]).ToArray();
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
    public void GraphMetadataRoundTripsWithExactIdentityAndAdjacencyOwnership()
    {
        var identityType = SqlType.Binary(GraphNodeId.EncodedLength);
        var node = new CatalogTable(new TableId(1), "nodes", 2, new PageId(2), [
            new CatalogColumn(new ColumnId(1), "payload", SqlType.Int, true),
            new CatalogColumn(new ColumnId(2), "$node_id", identityType, false,
                generatedAlways: CatalogGeneratedAlwaysKind.GraphIdentity, isHidden: true)
        ], graph: CatalogGraphTable.Node(new ColumnId(2), new IndexId(1)));
        var edge = new CatalogTable(new TableId(2), "edges", 2, new PageId(3), [
            new CatalogColumn(new ColumnId(1), "payload", SqlType.Int, true),
            new CatalogColumn(new ColumnId(2), "$edge_id", identityType, false,
                generatedAlways: CatalogGeneratedAlwaysKind.GraphIdentity, isHidden: true),
            new CatalogColumn(new ColumnId(3), "$from_id", identityType, false,
                generatedAlways: CatalogGeneratedAlwaysKind.GraphFromNode, isHidden: true),
            new CatalogColumn(new ColumnId(4), "$to_id", identityType, false,
                generatedAlways: CatalogGeneratedAlwaysKind.GraphToNode, isHidden: true)
        ], graph: CatalogGraphTable.Edge(new ColumnId(2), new IndexId(2), node.Id, new ColumnId(3),
            new IndexId(3), node.Id, new ColumnId(4), new IndexId(4)));
        var catalog = new CatalogDefinition([node, edge], [
            Index(1, node.Id, new ColumnId(2), unique: true),
            Index(2, edge.Id, new ColumnId(2), unique: true),
            Index(3, edge.Id, new ColumnId(3), unique: false),
            Index(4, edge.Id, new ColumnId(4), unique: false)
        ]);

        var decoded = CatalogCodec.Decode(CatalogCodec.Encode(catalog));

        decoded.Tables.Single(table => table.Id == node.Id).Graph.Should().BeEquivalentTo(node.Graph);
        decoded.Tables.Single(table => table.Id == edge.Id).Graph.Should().BeEquivalentTo(edge.Graph);
    }

    private static CatalogIndex Index(ulong id, TableId tableId, ColumnId columnId, bool unique) =>
        new(new IndexId(id), "i" + id, tableId, new PageId(id + 10), unique,
            [new CatalogIndexedColumn(columnId, SortDirection.Ascending, NullSortOrder.First)]);

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
