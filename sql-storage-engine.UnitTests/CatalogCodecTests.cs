using System.Buffers.Binary;
using System.Security.Cryptography;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.UnitTests;
public sealed class CatalogCodecTests
{
    private static CatalogDefinition Sample() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "t", 2, new sql_storage_engine.Models.Identifiers.PageId { Value = 3 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 }, "c", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(100,"Latin1_General_100_BIN2"), true)]) with { ObjectId = new Guid("11111111-2222-3333-4444-555555555555") }], [sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 5 },name: "i",tableId: new sql_storage_engine.Models.Identifiers.TableId { Value = 1 },pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 6 },isUnique: true,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 },SortDirection.Descending,NullSortOrder.First,"o")])]);
    // Retain the committed v10 golden oracle and legacy migration fixtures.
    private static byte[] Version10(CatalogDefinition catalog)
    {
        var bytes = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(catalog);
        var tail = sql_storage_engine.Exposures.Security.StorageSecurityStateExposure.Encode(catalog.Security).Length + 4;
        // These fixtures contain one table and no records preceding it.
        var legacy = bytes[..40].Concat(bytes[60..^tail]).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(legacy, 0x30544143);
        BinaryPrimitives.WriteUInt16LittleEndian(legacy.AsSpan(4), 10);
        return legacy;
    }

    [Test]
    public void TableAndIndexDefinitions_RoundTripThroughBootstrapFormat()
    {
        var decoded = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(Sample()));
        decoded.Tables.Single().Should().BeEquivalentTo(Sample().Tables.Single());
        decoded.Indexes.Single().Should().BeEquivalentTo(Sample().Indexes.Single());
    }

    [Test]
    public void SampleCatalog_ProducesCommittedGoldenBytes()
    {
        Convert.ToBase64String(SHA256.HashData(Version10(Sample()))).Should().Be("uzNbagABC7gBatD2cQTus3jcSAG+AyXcMinXj37OKIY=");
    }

    [Test]
    public void Version7CatalogWithoutTemporalMetadataRemainsReadable()
    {
        var version10 = Version10(Sample());
        const int indexRecordLength = 58;
        var graphMarkerOffset = version10.Length - indexRecordLength - 1;
        var version9 = version10[..graphMarkerOffset].Concat(version10[(graphMarkerOffset + 1)..]).ToArray();
        var temporalMarkerOffset = version9.Length - indexRecordLength - 1;
        var version7 = version9[..temporalMarkerOffset].Concat(version9[(temporalMarkerOffset + 1)..]).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(version7, 0x37544143);
        BinaryPrimitives.WriteUInt16LittleEndian(version7.AsSpan(4), 7);
        var decoded = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(version7);
        decoded.Tables.Single().SystemVersioning.Should().BeNull();
        decoded.Tables.Single().QualifiedName.Should().Be(Sample().Tables.Single().QualifiedName);
    }

    [Test]
    public void Version8SpatialIndexesRemainReadableWithAllSridCompatibility()
    {
        var table = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "places", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 2 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "point", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeGeometry(), true)]);
        var catalog = sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([table], [sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 },name: "pk",tableId: table.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 3 },isUnique: true,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)],storageKind: CatalogIndexStorageKind.Clustered,isPrimaryKey: true), sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 2 },name: "spatial",tableId: table.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 4 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.First)],catalogSpecializedIndexOptions: sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateSpatialCatalogSpecializedIndexOptions())]);
        var version10 = Version10(catalog);
        var tableOnlyLength = Version10(sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([table], [])).Length;
        var version9 = version10[..(tableOnlyLength - 1)].Concat(version10[tableOnlyLength..]).ToArray();
        var spatialSridMarkerOffset = version9.Length - 10;
        var version8 = version9[..spatialSridMarkerOffset].Concat(version9[(spatialSridMarkerOffset + 1)..]).ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(version8, 0x38544143);
        BinaryPrimitives.WriteUInt16LittleEndian(version8.AsSpan(4), 8);
        var decoded = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(version8);
        decoded.Indexes.Single(index => index.Method == CatalogIndexMethod.Spatial).SpecializedOptions!.SpatialSrid.Should().BeNull();
    }

    [Test]
    public void GraphMetadataRoundTripsWithExactIdentityAndAdjacencyOwnership()
    {
        var identityType = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateBinarySqlType(sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.EncodedLength);
        var node = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "nodes", 2, new sql_storage_engine.Models.Identifiers.PageId { Value = 2 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "payload", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "$node_id", identityType, false, generatedAlways: CatalogGeneratedAlwaysKind.GraphIdentity, isHidden: true)], catalogGraphTable: sql_storage_engine.Exposures.Catalog.CatalogGraphTableExposure.CreateCatalogGraphTableNode((new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }).Value,(new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 }).Value));
        var edge = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 2 }, "edges", 2, new sql_storage_engine.Models.Identifiers.PageId { Value = 3 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "payload", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "$edge_id", identityType, false, generatedAlways: CatalogGeneratedAlwaysKind.GraphIdentity, isHidden: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "$from_id", identityType, false, generatedAlways: CatalogGeneratedAlwaysKind.GraphFromNode, isHidden: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 }, "$to_id", identityType, false, generatedAlways: CatalogGeneratedAlwaysKind.GraphToNode, isHidden: true)], catalogGraphTable: sql_storage_engine.Exposures.Catalog.CatalogGraphTableExposure.CreateCatalogGraphTableEdge((new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }).Value,(new sql_storage_engine.Models.Identifiers.IndexId { Value = 2 }).Value,(node.Id).Value,(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }).Value,(new sql_storage_engine.Models.Identifiers.IndexId { Value = 3 }).Value,(node.Id).Value,(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 }).Value,(new sql_storage_engine.Models.Identifiers.IndexId { Value = 4 }).Value));
        var catalog = sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([node, edge], [Index(1,node.Id,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },unique: true), Index(2,edge.Id,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },unique: true), Index(3,edge.Id,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },unique: false), Index(4,edge.Id,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 },unique: false)]);
        var decoded = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(catalog));
        decoded.Tables.Single(table => table.Id == node.Id).Graph.Should().BeEquivalentTo(node.Graph);
        decoded.Tables.Single(table => table.Id == edge.Id).Graph.Should().BeEquivalentTo(edge.Graph);
    }

    private static CatalogIndex Index(ulong id, TableId tableId, ColumnId columnId, bool unique) => sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = id },name: "i" + id,tableId: tableId,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = id + 10 },isUnique: unique,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(columnId,SortDirection.Ascending,NullSortOrder.First)]);
    [Test]
    public void UnknownVersionAndEveryTruncation_AreRejected()
    {
        var encoded = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(Sample());
        BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(4), 99);
        ((Func<CatalogDefinition>)(() => sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(encoded))).Should().Throw<StorageFormatException>();
        encoded = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(Sample());
        for (var length = 0; length < encoded.Length; length++)
            ((Func<CatalogDefinition>)(() => sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(encoded.AsSpan(0, length)))).Should().Throw<StorageFormatException>($"length {length} is truncated");
    }

    [Test]
    public void InvalidPersistedCrossReference_IsReportedAsCorruption()
    {
        var catalog = Sample();
        var encoded = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(catalog);
        // The sample has no records after its 58-byte index record. Its table ID starts 13 bytes into that record.
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(encoded.Length - sql_storage_engine.Exposures.Security.StorageSecurityStateExposure.Encode(catalog.Security).Length - 4 - 45), 99);
        ((Func<CatalogDefinition>)(() => sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(encoded))).Should().Throw<StorageCorruptionException>();
    }
}