// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Tables;
using System.Buffers.Binary;
using System.Numerics;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class SqlServerGapClosureTests
{
    [Test]
    public void HierarchyIdSupportsFullLabelsNavigationOrderingAndBinaryRoundTrip()
    {
        var value = sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ParseSqlHierarchyId("/123456789012345678901234567890/-4.7/9/");
        sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ReadSqlHierarchyId(sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.WriteSqlHierarchyId(sqlHierarchyId: value)).Should().Be(value);
        sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ReadSqlHierarchyIdLevel(sqlHierarchyId: value).Should().Be(3);
        sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ReadSqlHierarchyIdAncestor(sqlHierarchyId: value,2).Should().Be(sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ParseSqlHierarchyId("/123456789012345678901234567890/"));
        sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ReparentSqlHierarchyId([value, sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ParseSqlHierarchyId("/123456789012345678901234567890/"), sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ParseSqlHierarchyId("/8/")]).Should().Be(sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ParseSqlHierarchyId("/8/-4.7/9/"));
        var root = sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ReadRootSqlHierarchyId();
        var left = sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ParseSqlHierarchyId("/1/");
        var right = sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ParseSqlHierarchyId("/2/");
        var middle = sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.CreateSqlHierarchyIdDescendant([root, left, right]);
        sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.CompareSqlHierarchyIds([middle, left]).Should().BePositive();
        sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.CompareSqlHierarchyIds([middle, right]).Should().BeNegative();
        ((Action)(() => sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ParseSqlHierarchyId("/01/"))).Should().Throw<FormatException>();
    }

    [Test]
    public void SpatialValuesSupportStructuredShapesWkbGmlAndTopology()
    {
        var polygon = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "POLYGON ((0 0,10 0,10 10,0 10,0 0))");
        var point = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "POINT (5 5)");
        var disjointLine = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "LINESTRING (20 0,30 0)");
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckValidSqlSpatial(sqlSpatial: polygon).Should().BeTrue();
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.MeasureSqlSpatialArea(sqlSpatial: polygon).Should().BeApproximately(100, 0.000001);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckContainingSqlSpatials(sqlSpatials: [polygon, point]).Should().BeTrue();
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckIntersectingSqlSpatials(sqlSpatials: [polygon, disjointLine]).Should().BeFalse();
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckEquivalentSqlSpatials(sqlSpatials: [sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatialWkb(kind: SqlSpatialKind.Geometry,value: sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.EncodeSqlSpatialWkb(sqlSpatial: polygon)), polygon]).Should().BeTrue();
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckEquivalentSqlSpatials(sqlSpatials: [polygon, sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "POLYGON ((10 0,10 10,0 10,0 0,5 0,10 0))")]).Should().BeTrue();
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckEquivalentSqlSpatials(sqlSpatials: [point, sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 0,wellKnownText: "POINT Z (5 5 99)")]).Should().BeTrue();
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.MeasureSqlSpatialDistance(sqlSpatials: [point, sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 1,wellKnownText: "POINT (5 5)")]).Should().BeNull();
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckIntersectingSqlSpatials(sqlSpatials: [point, sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geometry,srid: 1,wellKnownText: "POINT (5 5)")]).Should().BeNull();
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.DecodeSqlSpatial(value: sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.EncodeSqlSpatial(sqlSpatial: polygon),expectedKind: SqlSpatialKind.Geometry).Should().Be(polygon);
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.FormatSqlSpatialGml(sqlSpatial: polygon).Should().Contain("Polygon");
        var globe = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geography,srid: 4326,wellKnownText: "FULLGLOBE");
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CheckContainingSqlSpatials(sqlSpatials: [globe, sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geography,srid: 4326,wellKnownText: "POINT (0 0)")]).Should().BeTrue();
    }

    [Test]
    public async Task DefaultsIdentityComputedChecksSparseAndGeneratedColumnsExecuteDurably()
    {
        var path = TempDatabase("generated");
        try
        {
            TableId tableId;
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var table = await engine.CreateCatalogTableAsync("orders",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, catalogIdentity: sql_storage_engine.Exposures.Catalog.CatalogIdentityExposure.CreateCatalogIdentity(10,2)), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "quantity", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, defaultExpression: "2"), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "total", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, computedExpression: "[quantity] * 3", isComputedPersisted: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 }, "rv", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeRowVersion(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 5 }, "tag", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(20), true, isSparse: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 6 }, "sparse_values", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(), true, isColumnSet: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 7 }, "period_start", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTime2SqlType(), false, generatedAlways: CatalogGeneratedAlwaysKind.RowStart, isHidden: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 8 }, "sequence", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false, generatedAlways: CatalogGeneratedAlwaysKind.SequenceNumberStart, isHidden: true)],[sql_storage_engine.Exposures.Catalog.CatalogCheckConstraintExposure.CreateCatalogCheckConstraint("CK_orders_quantity","quantity > 0")]);
                tableId = table.Id;
                var storage = await engine.OpenTableAsync(table.Id);
                var firstId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("priority"), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()]));
                var first = (await storage.GetAsync(firstId))!.Row;
                first.Values[0].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(10));
                first.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2));
                first.Values[2].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(6));
                first.Values[3].Should().BeOfType<BinarySqlValue>();
                ((TextSqlValue)first.Values[5]).Value.Should().Contain("<tag>priority</tag>");
                first.Values[6].Should().BeOfType<DateTimeSqlValue>();
                first.Values[7].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1));
                await ((Func<Task>)(async () => await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(-1), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()])))).Should().ThrowAsync<ArgumentException>();
            }

            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path))
            {
                var storage = await reopened.OpenTableAsync(tableId);
                var nextId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(4), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()]));
                var next = (await storage.GetAsync(nextId))!.Row;
                next.Values[0].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(14), "failed SQL inserts consume an identity value");
                next.Values[2].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(12));
                next.Values[7].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3), "generated sequence values are also consumed by failed inserts");
            }
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task Decimal38IdentityIsNotLimitedByClrInt64()
    {
        var path = TempDatabase("decimal-identity");
        var seed = BigInteger.Parse("12345678901234567890123456789012345670");
        try
        {
            TableId tableId;
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var table = await engine.CreateCatalogTableAsync("large_ids",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(38,0), false, catalogIdentity: sql_storage_engine.Exposures.Catalog.CatalogIdentityExposure.CreateCatalogIdentity(seed,BigInteger.One))]);
                tableId = table.Id;
                var rowId = await (await engine.OpenTableAsync(table.Id)).InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue()]));
                (await (await engine.OpenTableAsync(table.Id)).GetAsync(rowId))!.Row.Values[0].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(seed,0));
            }

            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path))
            {
                var storage = await reopened.OpenTableAsync(tableId);
                var rowId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue()]));
                (await storage.GetAsync(rowId))!.Row.Values[0].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(seed + 1,0));
            }

            ((Func<CatalogColumn>)(() => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "bad", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(38,1), false, catalogIdentity: sql_storage_engine.Exposures.Catalog.CatalogIdentityExposure.CreateCatalogIdentity()))).Should().Throw<ArgumentException>();
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task AlwaysEncryptedColumnsRoundTripAndRespectIndexRestrictions()
    {
        var path = TempDatabase("encrypted");
        sql_storage_engine.Exposures.Rows.SqlColumnEncryptionExposure.RegisterKey("det_key", Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        sql_storage_engine.Exposures.Rows.SqlColumnEncryptionExposure.RegisterKey("rnd_key", Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        try
        {
            TableId tableId;
            RowId firstId;
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var table = await engine.CreateCatalogTableAsync("secrets",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "secret", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(40,"Latin1_General_100_BIN2"), false, catalogColumnEncryption: sql_storage_engine.Exposures.Catalog.CatalogColumnEncryptionExposure.CreateCatalogColumnEncryption("det_key",CatalogEncryptionType.Deterministic)), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "nonce", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, catalogColumnEncryption: sql_storage_engine.Exposures.Catalog.CatalogColumnEncryptionExposure.CreateCatalogColumnEncryption("rnd_key",CatalogEncryptionType.Randomized))]);
                tableId = table.Id;
                await engine.CreateCatalogIndexAsync("PK_secrets",table.Id,true,[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)],sql_storage_engine.Exposures.Catalog.CatalogBTreeIndexOptionsExposure.CreateCatalogBTreeIndexOptions(CatalogIndexStorageKind.Clustered,isPrimaryKey: true));
                var secretIndex = await engine.CreateCatalogIndexAsync("IX_secret",table.Id,false,[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.First)]);
                var storage = await engine.OpenTableAsync(table.Id);
                firstId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("classified"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7)]));
                await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("classified"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7)]));
                (await (await engine.OpenIndexAsync(secretIndex.Id)).FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("classified")])).Should().HaveCount(2);
                await ((Func<Task>)(async () => await engine.CreateCatalogIndexAsync("IX_nonce",table.Id,false,[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },SortDirection.Ascending,NullSortOrder.First)]))).Should().ThrowAsync<ArgumentException>();
            }

            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path))
            {
                var row = await (await reopened.OpenTableAsync(tableId)).GetAsync(firstId);
                row!.Row.Values.Should().Equal(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("classified"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7));
                reopened.ReadCatalog().ReadTables().Single().Columns[1].Encryption!.EncryptionType.Should().Be(CatalogEncryptionType.Deterministic);
            }
        }
        finally
        {
            sql_storage_engine.Exposures.Rows.SqlColumnEncryptionExposure.Unregister("det_key");
            sql_storage_engine.Exposures.Rows.SqlColumnEncryptionExposure.Unregister("rnd_key");
            DeleteDatabase(path);
        }
    }

    [Test]
    public void ExtendedColumnFacetsPersistAndMaskingFunctionsExecute()
    {
        CatalogColumn[] columns = [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "stream_id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeUniqueIdentifier(), false, defaultExpression: "newid()", isRowGuidCol: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "payload", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeVarBinaryMax(), true, isFileStream: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "nickname", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(30), true, maskingFunction: "partial(1,'XXXX',1)"), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 }, "sparse_values", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(), true, isColumnSet: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 5 }, "period_end", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTime2SqlType(), false, generatedAlways: CatalogGeneratedAlwaysKind.RowEnd, isHidden: true)];
        var table = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "files", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, columns);
        var catalog = sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([table], [sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 },name: "UQ_files_stream_id",tableId: table.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 2 },isUnique: true,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)])]);
        var decoded = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(catalog));
        decoded.Tables.Single().Columns.Should().BeEquivalentTo(columns);
        sql_storage_engine.Exposures.Rows.SqlDataMaskingExposure.Apply(columns[2],sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("Callum")).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("CXXXXm"));
    }

    [Test]
    public async Task JsonAndXmlIndexesSupportAdvancedPathsAndPersistTheirKinds()
    {
        var path = TempDatabase("document-index");
        try
        {
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var table = await engine.CreateCatalogTableAsync("documents",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "body", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "xml_body", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(), false)]);
                await engine.CreateCatalogIndexAsync("PK_documents",table.Id,true,[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)],sql_storage_engine.Exposures.Catalog.CatalogBTreeIndexOptionsExposure.CreateCatalogBTreeIndexOptions(CatalogIndexStorageKind.Clustered,isPrimaryKey: true));
                var jsonValue = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("{\"items\":[0,1,2,3],\"odd.name\":9}");
                var xmlValue = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<root xmlns:d=\"urn:documents\"><item id=\"1\"/><item id=\"2\"/>" + "<meta>x</meta><!--note--><?audit ok?>" + "<d:item d:code=\"N2\">namespaced</d:item></root>");
                var storage = await engine.OpenTableAsync(table.Id);
                var rowId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), jsonValue, xmlValue]));
                var json = await engine.CreateSpecializedCatalogIndexAsync("IX_json",table.Id,sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.First),sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions());
                var xml = await engine.CreateSpecializedCatalogIndexAsync("IX_xml",table.Id,sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },SortDirection.Ascending,NullSortOrder.First),sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateXmlCatalogSpecializedIndexOptions(CatalogXmlIndexKind.Selective, new Dictionary<string, string> { ["d"] = "urn:documents" }, "/root/item", "/root/item/@id", "/root/meta", "/root/d:item/@d:code"));
                var primaryXml = await engine.CreateSpecializedCatalogIndexAsync("IX_xml_primary",table.Id,sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },SortDirection.Ascending,NullSortOrder.First),sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateXmlCatalogSpecializedIndexOptions(CatalogXmlIndexKind.Primary, new Dictionary<string, string> { ["d"] = "urn:documents" }));
                var jsonIndex = await engine.OpenIndexAsync(json.Id);
                var xmlIndex = await engine.OpenIndexAsync(xml.Id);
                var primaryXmlIndex = await engine.OpenIndexAsync(primaryXml.Id);
                (await jsonIndex.FindAsync([jsonValue])).Should().Equal(rowId);
                (await xmlIndex.FindAsync([xmlValue])).Should().Equal(rowId);
                (await jsonIndex.FindJsonPathAsync("$.items[1 to 2]",sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1))).Should().Equal(rowId);
                (await jsonIndex.FindJsonPathAsync("$.\"odd.name\"",sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(9))).Should().Equal(rowId);
                (await jsonIndex.FindJsonPathExistsAsync("$.items[1 to 2]")).Should().Equal(rowId);
                (await jsonIndex.FindJsonPathAsync("$.items[*]",sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2))).Should().Equal(rowId);
                (await jsonIndex.FindJsonPathAsync("$.items[last, 0]",sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3))).Should().Equal(rowId);
                (await xmlIndex.FindXmlPathAsync("/root/meta", "x")).Should().Equal(rowId);
                (await xmlIndex.FindXmlPathAsync("/root/item")).Should().Equal(rowId);
                (await xmlIndex.FindXmlPathAsync("/root/item/@id", "2")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/item/@id", "2")).Should().Equal(rowId);
                (await xmlIndex.FindXmlPathAsync("/root/d:item/@d:code", "N2")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/d:item/@d:code", "N2")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/d:item/text()", "namespaced")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/comment()", "note")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/processing-instruction()", "ok")).Should().Equal(rowId);
                var tooDeep = string.Concat(Enumerable.Repeat("<n>", 129)) + "x" + string.Concat(Enumerable.Repeat("</n>", 129));
                await ((Func<Task>)(async () => await storage.UpdateAsync(rowId,RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 2, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(tooDeep) }])))).Should().ThrowAsync<TableMutationException>();
                (await xmlIndex.FindXmlPathAsync("/root/meta", "x")).Should().Equal(rowId);
                await storage.UpdateAsync(rowId,RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("{\"items\":[5,6],\"odd.name\":10}") }, new ColumnUpdate { ColumnIndex = 2, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<root><item>three</item><meta>y</meta></root>") }]));
                (await jsonIndex.FindJsonPathAsync("$.items[1 to 2]",sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1))).Should().BeEmpty();
                (await jsonIndex.FindJsonPathAsync("$.items[1 to 2]",sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(6))).Should().Equal(rowId);
                (await xmlIndex.FindXmlPathAsync("/root/meta", "x")).Should().BeEmpty();
                (await xmlIndex.FindXmlPathAsync("/root/meta", "y")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/item/@id", "2")).Should().BeEmpty();
                (await primaryXmlIndex.FindXmlPathAsync("/root/item", "three")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/item/text()", "three")).Should().Equal(rowId);
            }

            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var persistedXml = reopened.ReadCatalog().ReadIndexes().Single(index => index.Method == CatalogIndexMethod.Xml && index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Selective);
            persistedXml.SpecializedOptions!.XmlIndexKind.Should().Be(CatalogXmlIndexKind.Selective);
            persistedXml.SpecializedOptions.XmlNamespaces.Should().Contain("d", "urn:documents");
            var reopenedXml = await reopened.OpenIndexAsync(persistedXml.Id);
            var persistedRows = await reopenedXml.FindXmlPathAsync("/root/meta", "y");
            persistedRows.Should().ContainSingle();
            var persistedPrimaryXml = reopened.ReadCatalog().ReadIndexes().Single(index => index.Method == CatalogIndexMethod.Xml && index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Primary);
            var reopenedPrimaryXml = await reopened.OpenIndexAsync(persistedPrimaryXml.Id);
            (await reopenedPrimaryXml.FindXmlPathAsync("/root/item", "three")).Should().ContainSingle();
            var persistedJson = reopened.ReadCatalog().ReadIndexes().Single(index => index.Method == CatalogIndexMethod.Json);
            var reopenedJson = await reopened.OpenIndexAsync(persistedJson.Id);
            (await reopenedJson.FindJsonPathAsync("$.items[1 to 2]", sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(6))).Should().ContainSingle();
            var persistedTable = reopened.ReadCatalog().ReadTables().Single(table => table.Name == "documents");
            (await (await reopened.OpenTableAsync(persistedTable.Id)).DeleteAsync(persistedRows.Single())).Deleted.Should().BeTrue();
            (await reopenedXml.FindXmlPathAsync("/root/meta", "y")).Should().BeEmpty();
            (await reopenedPrimaryXml.FindXmlPathAsync("/root/item", "three")).Should().BeEmpty();
            (await reopenedJson.FindJsonPathAsync("$.items[1 to 2]", sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(6))).Should().BeEmpty();
            ((Func<CatalogSpecializedIndexOptions>)(() => sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions("$.items[2 to 1]"))).Should().Throw<ArgumentException>();
            ((Func<CatalogSpecializedIndexOptions>)(() => sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateXmlCatalogSpecializedIndexOptions(CatalogXmlIndexKind.Selective,"root/["))).Should().Throw<ArgumentException>();
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public void JsonPathsSupportAnsiListsEscapesEmptyKeysAndDepthLimits()
    {
        var table = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "json_paths", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "body", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(), false)]);
        var index = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 },name: "IX_json_paths",tableId: table.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 2 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)],catalogSpecializedIndexOptions: sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions("$.items[last, 0]","$.\"odd\\u002ename\"","$.\"\""));
        var entries = sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.EncodeEntries(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("{\"items\":[0,1,2],\"odd.name\":9,\"\":true}")]),table,index);
        entries.Should().Contain(sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.EncodeJsonPathValue(index,"$.items[last, 0]",sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)));
        entries.Should().Contain(sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.EncodeJsonPathValue(index,"$.items[last, 0]",sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(0)));
        entries.Should().Contain(sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.EncodeJsonPathValue(index,"$.\"odd\\u002ename\"",sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(9)));
        entries.Should().Contain(sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.EncodeJsonPathValue(index,"$.\"\"",sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true)));
        var recursive = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 2 },name: "IX_json_recursive",tableId: table.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 3 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)],catalogSpecializedIndexOptions: sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions());
        var recursiveEntries = sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.EncodeEntries(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("{\"items\":[0,1,2],\"odd.name\":9}")]),table,recursive);
        recursiveEntries.Should().Contain(sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.EncodeJsonPathValue(recursive,"$.items[1]",sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)));
        recursiveEntries.Should().Contain(sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.EncodeJsonPathExists(recursive,"$.\"odd.name\""));
        var tooDeep = "$" + string.Concat(Enumerable.Repeat(".a", 129));
        ((Func<CatalogSpecializedIndexOptions>)(() => sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions(tooDeep))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void NativeJsonUsesSqlServerDocumentLimitsInsteadOfRuntimeDefaults()
    {
        var depth100 = string.Concat(Enumerable.Repeat("{\"a\":", 100)) + "0" + new string ('}', 100);
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.CheckSqlTypeValue(sqlType: sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(depth100),"body");
        var definition = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition([sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"body",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(),false)]);
        sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(depth100)]),definition),definition).Values.Should().ContainSingle();
        var depth129 = string.Concat(Enumerable.Repeat("{\"a\":", 129)) + "0" + new string ('}', 129);
        ((Action)(() => sql_storage_engine.Exposures.Rows.SqlTypeExposure.CheckSqlTypeValue(sqlType: sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(depth129),"body"))).Should().Throw<ArgumentException>();
        var oversizedKey = "{\"" + new string ('k', 7_999) + "\":0}";
        ((Action)(() => sql_storage_engine.Exposures.Rows.SqlTypeExposure.CheckSqlTypeValue(sqlType: sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(oversizedKey),"body"))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task DatabaseDefaultCollationIsResolvedAtDdlAndPersists()
    {
        var path = TempDatabase("collation");
        const string collation = "Latin1_General_100_CI_AI_SC_UTF8";
        try
        {
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path,sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageEngineOptions()with{DefaultCollation = collation }))
            {
                var table = await engine.CreateCatalogTableAsync("names",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "name", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(100), false)]);
                table.Columns.Single().Type.Collation.Should().Be(collation);
            }

            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            reopened.ReadCatalog().ReadDefaultCollation().Should().Be(collation);
            reopened.ReadCatalog().ReadTables().Single().Columns.Single().Type.Collation.Should().Be(collation);
            ((Func<sql_storage_engine.Models.Rows.SqlCollation>)(() => sql_storage_engine.Exposures.Rows.SqlCollationExposure.ParseSqlCollation("Unknown_Collation_CI_AS"))).Should().Throw<ArgumentException>();
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public void CurrentVectorJsonAndSparseRestrictionsAreEnforced()
    {
        ((Func<SqlType>)(() => sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(1_999,SqlVectorBaseType.Float32))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<SqlType>)(() => sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(1_999,SqlVectorBaseType.Float16))).Should().Throw<ArgumentOutOfRangeException>();
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(1_998,SqlVectorBaseType.Float16).VectorDimensions.Should().Be(1_998);
        var json = sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"document",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(),false);
        sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CheckColumnDefinitionValue(columnDefinition: json,sqlValue: sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("{}"));
        sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CheckColumnDefinitionValue(columnDefinition: json,sqlValue: sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("[]"));
        foreach (var scalar in new[]
        {
            "true",
            "null",
            "42",
            "\"text\""
        }

        )
            ((Action)(() => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CheckColumnDefinitionValue(columnDefinition: json,sqlValue: sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(scalar)))).Should().Throw<ArgumentException>();
        ((Func<CatalogColumn>)(() => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), true, defaultExpression: "0", isSparse: true))).Should().Throw<ArgumentException>();
        ((Func<CatalogColumn>)(() => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), true, catalogIdentity: sql_storage_engine.Exposures.Catalog.CatalogIdentityExposure.CreateCatalogIdentity(), isSparse: true))).Should().Throw<ArgumentException>();
        ((Func<CatalogColumn>)(() => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), true, computedExpression: "1", isSparse: true))).Should().Throw<ArgumentException>();
        var emptyColumnSetTable = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "future_sparse", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "values", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(), true, isColumnSet: true)]);
        emptyColumnSetTable.Columns.Should().ContainSingle(column => column.IsColumnSet);
        ((Func<CatalogTableType>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("dbo", "bad_sparse_type", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), true, isSparse: true)]))).Should().Throw<ArgumentException>();
    }

    [Test]
    public async Task XmlColumnSetIsUpdatableProjectedAndNotAuthoritativeStorage()
    {
        var path = TempDatabase("column-set");
        try
        {
            TableId tableId;
            RowId rowId;
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var table = await engine.CreateCatalogTableAsync("properties",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "quantity", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), true, isSparse: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "label", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(20), true, isSparse: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 }, "properties", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(), true, isColumnSet: true)]);
                tableId = table.Id;
                var storage = await engine.OpenTableAsync(table.Id);
                rowId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<quantity>7</quantity><label>first</label>")]));
                var inserted = (await storage.GetAsync(rowId))!.Row;
                inserted.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7));
                inserted.Values[2].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("first"));
                ((TextSqlValue)inserted.Values[3]).Value.Should().Be("<quantity>7</quantity><label>first</label>");
                await storage.UpdateAsync(rowId,RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 3, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<label>second</label>") }]));
                var updated = (await storage.GetAsync(rowId))!.Row;
                updated.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue());
                updated.Values[2].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("second"));
                ((TextSqlValue)updated.Values[3]).Value.Should().Be("<label>second</label>");
            }

            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var durable = (await (await reopened.OpenTableAsync(tableId)).GetAsync(rowId))!.Row;
            durable.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue());
            durable.Values[2].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("second"));
            ((TextSqlValue)durable.Values[3]).Value.Should().Be("<label>second</label>");
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task DynamicDataMaskingValidatesFunctionsAndHonorsReadPermissionContext()
    {
        ((Func<CatalogColumn>)(() => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "bad", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), true, maskingFunction: "email()"))).Should().Throw<ArgumentException>();
        ((Func<CatalogColumn>)(() => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "bad", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(10), true, maskingFunction: "random(10,1)"))).Should().Throw<ArgumentException>();
        var path = TempDatabase("masking");
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var table = await engine.CreateCatalogTableAsync("members",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "name", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(20), false, maskingFunction: "partial(1,\"XXXX\",1)"), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "email", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(100), false, maskingFunction: "email()")]);
            var storage = await engine.OpenTableAsync(table.Id);
            var rowId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("Callum"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("callum@example.test")]));
            (await storage.GetAsync(rowId))!.Row.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("Callum"));
            var masked = (await storage.GetAsync(rowId,sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageReadOptions()))!.Row;
            masked.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("CXXXXm"));
            masked.Values[2].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("cXXX@XXXX.com"));
            var columnGrant = sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageReadOptions(unmaskedColumns: [new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }]);
            var partial = (await storage.GetAsync(rowId,columnGrant))!.Row;
            partial.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("Callum"));
            partial.Values[2].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("cXXX@XXXX.com"));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public void DecimalExpressionsUseSqlScaleRulesAndLosslessExactComparison()
    {
        ((DecimalSqlValue)sql_storage_engine.Exposures.Rows.SqlExpressionExposure.Evaluate("1.20 + 3.456")).Value.Should().Be(sql_storage_engine.Exposures.Rows.SqlDecimalExposure.CreateSqlDecimal(4_656,3));
        ((DecimalSqlValue)sql_storage_engine.Exposures.Rows.SqlExpressionExposure.Evaluate("1.0 / 3.0")).Value.Should().Be(sql_storage_engine.Exposures.Rows.SqlDecimalExposure.CreateSqlDecimal(333_333,6));
        sql_storage_engine.Exposures.Rows.SqlExpressionExposure.Evaluate("9007199254740993 = 9007199254740992.0").Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(false));
        sql_storage_engine.Exposures.Rows.SqlExpressionExposure.Evaluate("1 + '2'").Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3));
        ((Func<SqlValue>)(() => sql_storage_engine.Exposures.Rows.SqlExpressionExposure.Evaluate("1 + 'not-a-number'"))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void LegacyBinAndBin2CollationsHaveDistinctUnicodeOrdering()
    {
        sql_storage_engine.Models.Rows.SqlCollation legacy = sql_storage_engine.Exposures.Rows.SqlCollationExposure.ParseSqlCollation("Latin1_General_100_BIN");
        sql_storage_engine.Models.Rows.SqlCollation modern = sql_storage_engine.Exposures.Rows.SqlCollationExposure.ParseSqlCollation("Latin1_General_100_BIN2");
        const string byteFirst = "A\u0100";
        const string codePointFirst = "A\u0002";
        sql_storage_engine.Exposures.Rows.SqlCollationExposure.CalculateSqlCollationSortKey(sqlCollation: legacy,byteFirst,unicode: true).AsSpan().SequenceCompareTo(sql_storage_engine.Exposures.Rows.SqlCollationExposure.CalculateSqlCollationSortKey(sqlCollation: legacy,codePointFirst,unicode: true)).Should().BeNegative();
        sql_storage_engine.Exposures.Rows.SqlCollationExposure.CalculateSqlCollationSortKey(sqlCollation: modern,byteFirst,unicode: true).AsSpan().SequenceCompareTo(sql_storage_engine.Exposures.Rows.SqlCollationExposure.CalculateSqlCollationSortKey(sqlCollation: modern,codePointFirst,unicode: true)).Should().BePositive();
    }

    [Test]
    public void SparseNullsOmitFixedSlotsAndVariableDirectoryEntries()
    {
        var regular = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition(Enumerable.Range(1, 8).Select(index => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = (ulong)index },$"c{index}",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),true)));
        var sparse = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition(Enumerable.Range(1, 8).Select(index => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = (ulong)index },$"c{index}",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),true,isSparse: true)));
        var nullRow = RowExposure.CreateRow(Enumerable.Repeat(sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), 8));
        var regularBytes = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(nullRow,regular);
        var sparseBytes = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(nullRow,sparse);
        sparseBytes.Should().HaveCount(sql_storage_engine.Exposures.Rows.RowCodecExposure.HeaderLength + 1);
        sparseBytes.Length.Should().BeLessThan(regularBytes.Length);
        sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sparseBytes,sparse).Values.Should().OnlyContain(value => value.IsNull);
        var populated = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), ..Enumerable.Repeat(sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), 7)]);
        sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(populated,sparse),sparse).Values.Should().Equal(populated.Values);
    }

    [Test]
    public async Task IgnoreDuplicateKeySkipsRowsAndReturnsAWarning()
    {
        var path = TempDatabase("ignore-duplicate");
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var table = await engine.CreateCatalogTableAsync("codes",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "code", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "description", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(20), false)]);
            await engine.CreateCatalogIndexAsync("UX_codes",table.Id,true,[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)],sql_storage_engine.Exposures.Catalog.CatalogBTreeIndexOptionsExposure.CreateCatalogBTreeIndexOptions(ignoreDuplicateKey: true));
            var storage = await engine.OpenTableAsync(table.Id);
            await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("first")]));
            var skipped = await storage.TryInsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("second")]));
            skipped.Inserted.Should().BeFalse();
            skipped.RowId.Should().BeNull();
            skipped.Warnings.Should().ContainSingle().Which.Should().Contain("UX_codes");
            var rows = new List<StoredRow>();
            await foreach (var row in storage.ScanAsync())
                rows.Add(row);
            rows.Should().ContainSingle();
            await ((Func<Task>)(async () => await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("strict wrapper")])))).Should().ThrowAsync<DuplicateKeyIgnoredException>();
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task VariableWidthIndexesEnforceActualRatherThanDeclaredKeyWidth()
    {
        var wideTable = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "wide_keys", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(8_000,"Latin1_General_100_BIN2"), false)]);
        var wideIndex = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 },name: "IX_wide_keys",tableId: wideTable.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 2 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)]);
        sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([wideTable], [wideIndex]).Indexes.Should().ContainSingle();
        var fixedTable = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 2 }, "fixed_keys", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 3 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateCharSqlType(901,"Latin1_General_100_BIN2"), false)]);
        var oversizedClustered = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 2 },name: "CX_fixed_keys",tableId: fixedTable.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 4 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)],storageKind: CatalogIndexStorageKind.Clustered);
        ((Func<CatalogDefinition>)(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([fixedTable], [oversizedClustered]))).Should().Throw<ArgumentException>();
        var path = TempDatabase("runtime-key-width");
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var table = await engine.CreateCatalogTableAsync("keys",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(8_000,"Latin1_General_100_BIN2"), false)]);
            await engine.CreateCatalogIndexAsync("IX_keys",table.Id,false,[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)]);
            var storage = await engine.OpenTableAsync(table.Id);
            await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('a', 1_700))]));
            await ((Func<Task>)(async () => await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('b', 1_701))])))).Should().ThrowAsync<TableMutationException>();
            var rows = new List<StoredRow>();
            await foreach (var row in storage.ScanAsync())
                rows.Add(row);
            rows.Should().ContainSingle();
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public void SpecializedIndexesApplyTheirDistinctClusteringKeyAndSourceRules()
    {
        var keyColumns = Enumerable.Range(1, 16).Select(position => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = (ulong)position }, $"k{position}", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTinyInt(), false)).ToArray();
        var indexedKey = keyColumns.Select(column => sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(column.Id, SortDirection.Ascending, NullSortOrder.First)).ToArray();
        var jsonTable = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "json_docs", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, [..keyColumns, sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 17 }, "body", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(), false)]);
        var primary = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 }, name: "PK_json_docs", tableId: jsonTable.Id, pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 2 }, isUnique: true, columns: indexedKey, storageKind: CatalogIndexStorageKind.Clustered, isPrimaryKey: true);
        var json = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 2 },name: "IX_json_docs",tableId: jsonTable.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 3 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 17 },SortDirection.Ascending,NullSortOrder.First)],catalogSpecializedIndexOptions: sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions());
        sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([jsonTable], [primary, json]).Indexes.Should().HaveCount(2);
        var xmlTable = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 2 }, "xml_docs", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 4 }, [..keyColumns, sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 17 }, "body", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(), false)]);
        var xmlPrimary = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 3 }, name: "PK_xml_docs", tableId: xmlTable.Id, pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 5 }, isUnique: true, columns: indexedKey, storageKind: CatalogIndexStorageKind.Clustered, isPrimaryKey: true);
        var xml = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 4 },name: "IX_xml_docs",tableId: xmlTable.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 6 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 17 },SortDirection.Ascending,NullSortOrder.First)],catalogSpecializedIndexOptions: sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateXmlCatalogSpecializedIndexOptions());
        ((Func<CatalogDefinition>)(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([xmlTable], [xmlPrimary, xml]))).Should().Throw<ArgumentException>();
        var computedJson = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 3 }, "computed_json", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 7 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "body", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(), true, computedExpression: "NULL")]);
        var computedPrimary = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 5 },name: "PK_computed_json",tableId: computedJson.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 8 },isUnique: true,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)],storageKind: CatalogIndexStorageKind.Clustered,isPrimaryKey: true);
        var computedIndex = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 6 },name: "IX_computed_json",tableId: computedJson.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 9 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.First)],catalogSpecializedIndexOptions: sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions());
        ((Func<CatalogDefinition>)(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([computedJson], [computedPrimary, computedIndex]))).Should().Throw<ArgumentException>();
        var computedXml = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 4 }, "computed_xml", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 10 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "body", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(), true, computedExpression: "NULL")]);
        var computedXmlPrimary = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 7 },name: "PK_computed_xml",tableId: computedXml.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 11 },isUnique: true,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)],storageKind: CatalogIndexStorageKind.Clustered,isPrimaryKey: true);
        CatalogIndex Selective(IndexId id, string name, PageId root) => sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: id,name: name,tableId: computedXml.Id,pageId: root,isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.First)],catalogSpecializedIndexOptions: sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateXmlCatalogSpecializedIndexOptions(CatalogXmlIndexKind.Selective,"/root/value"));
        ((Func<CatalogDefinition>)(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([computedXml], [computedXmlPrimary, Selective(new sql_storage_engine.Models.Identifiers.IndexId { Value = 8 },"SXI_computed_xml",new sql_storage_engine.Models.Identifiers.PageId { Value = 12 })]))).Should().Throw<ArgumentException>();
        var ordinaryXml = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 5 }, "ordinary_xml", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 13 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "body", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(), true)]);
        var ordinaryPrimary = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 9 },name: "PK_ordinary_xml",tableId: ordinaryXml.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 14 },isUnique: true,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)],storageKind: CatalogIndexStorageKind.Clustered,isPrimaryKey: true);
        CatalogIndex OrdinarySelective(IndexId id, string name, PageId root) => sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: id,name: name,tableId: ordinaryXml.Id,pageId: root,isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.First)],catalogSpecializedIndexOptions: sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateXmlCatalogSpecializedIndexOptions(CatalogXmlIndexKind.Selective,"/root/value"));
        ((Func<CatalogDefinition>)(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([ordinaryXml], [ordinaryPrimary, OrdinarySelective(new sql_storage_engine.Models.Identifiers.IndexId { Value = 10 },"SXI_one",new sql_storage_engine.Models.Identifiers.PageId { Value = 15 }), OrdinarySelective(new sql_storage_engine.Models.Identifiers.IndexId { Value = 11 },"SXI_two",new sql_storage_engine.Models.Identifiers.PageId { Value = 16 })]))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void UserDefinedTableValuesEnforceUniqueIndexesAndIgnoreDuplicateKey()
    {
        CatalogColumn[] columns = [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "label", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(20), false)];
        var key = sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First);
        var strict = sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("dbo", "StrictIds", columns, [sql_storage_engine.Exposures.Catalog.CatalogTableTypeIndexExposure.CreateCatalogTableTypeIndex("UX_id", false, true, [key])]);
        Row[] duplicates = [RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("first")]), RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("second")])];
        ((Func<SqlValue>)(() => sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTableSqlValue(strict,duplicates))).Should().Throw<ArgumentException>();
        var ignoring = sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("dbo", "IgnoringIds", columns, [sql_storage_engine.Exposures.Catalog.CatalogTableTypeIndexExposure.CreateCatalogTableTypeIndex("UX_id", false, true, [key], ignoreDuplicateKey: true)]);
        ((TableSqlValue)sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTableSqlValue(ignoring,duplicates)).Rows.Should().ContainSingle();
        var computed = sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("dbo", "ComputedBatch", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "result", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, computedExpression: "intermediate + 1"), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "intermediate", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, computedExpression: "source * 2"), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "source", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)]);
        ((TableSqlValue)sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTableSqlValue(computed,[RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(5)])])).Rows.Single().Values.Should().Equal(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(11), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(10), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(5));
        var wideIndex = sql_storage_engine.Exposures.Catalog.CatalogTableTypeIndexExposure.CreateCatalogTableTypeIndex("IX_wide", false, false, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)]);
        var wide = sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("dbo", "WideDiskBatch", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(8_000), false)], [wideIndex]);
        ((Func<SqlValue>)(() => sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTableSqlValue(wide,[RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('x', 1_701))])]))).Should().Throw<ArgumentException>();
        var versioned = sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("dbo", "VersionedBatch", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "rv", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeRowVersion(), false)]);
        var versions = ((TableSqlValue)sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTableSqlValue(versioned,[RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue()]), RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()])])).Rows.Select(row => sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadBinarySqlValue(binarySqlValue: ((BinarySqlValue)row.Values[1])).ToArray()).ToArray();
        BinaryPrimitives.ReadUInt64BigEndian(versions[0]).Should().Be(1);
        BinaryPrimitives.ReadUInt64BigEndian(versions[1]).Should().Be(2);
    }

    [Test]
    public void MemoryOptimizedTableTypesRejectUnsupportedTypesAndFacets()
    {
        var key = sql_storage_engine.Exposures.Catalog.CatalogTableTypeIndexExposure.CreateCatalogTableTypeIndex("IX_id", false, false, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)]);
        ((Func<CatalogTableType>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("dbo", "VectorBatch", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "embedding", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(3), true)], [key], isMemoryOptimized: true))).Should().Throw<ArgumentException>();
        ((Func<CatalogTableType>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("dbo", "IdentityBatch", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, catalogIdentity: sql_storage_engine.Exposures.Catalog.CatalogIdentityExposure.CreateCatalogIdentity(10,1))], [key], isMemoryOptimized: true))).Should().Throw<ArgumentException>();
        var nullablePrimary = sql_storage_engine.Exposures.Catalog.CatalogTableTypeIndexExposure.CreateCatalogTableTypeIndex("PK_id", true, true, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)]);
        ((Func<CatalogTableType>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("dbo", "NullableKeyBatch", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), true)], [nullablePrimary], isMemoryOptimized: true))).Should().Throw<ArgumentException>();
        var oversizedOrdered = sql_storage_engine.Exposures.Catalog.CatalogTableTypeIndexExposure.CreateCatalogTableTypeIndex("IX_wide", false, false, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)]);
        ((Func<CatalogTableType>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("dbo", "WideOrderedBatch", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(2_501), false)], [oversizedOrdered], isMemoryOptimized: true))).Should().Throw<ArgumentException>();
        var hashPrimary = sql_storage_engine.Exposures.Catalog.CatalogTableTypeIndexExposure.CreateCatalogTableTypeIndex("PK_wide", true, true, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)], CatalogIndexStorageKind.Hash, bucketCount: 16);
        var wideHash = sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("dbo", "WideHashBatch", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(8_000), false)], [hashPrimary], isMemoryOptimized: true);
        ((TableSqlValue)sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTableSqlValue(wideHash,[RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('x', 3_000))])])).Rows.Should().ContainSingle();
        ((Func<CatalogTableType>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("dbo", "DefaultBatch", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, defaultExpression: "1")], [key], isMemoryOptimized: true))).Should().Throw<ArgumentException>();
        ((Func<CatalogTableTypeIndex>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableTypeIndexExposure.CreateCatalogTableTypeIndex("IX_too_many_buckets", false, false, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)], CatalogIndexStorageKind.Hash, bucketCount: 1_073_741_825))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void CatalogDependencyChecksBindWholeIdentifiers()
    {
        CatalogColumn[] columns = [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "v", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(3), true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)];
        var allowed = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "vectors", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, columns, [sql_storage_engine.Exposures.Catalog.CatalogCheckConstraintExposure.CreateCatalogCheckConstraint("CK_value","'v' = 'v' AND value > 0")]);
        allowed.CheckConstraints.Should().ContainSingle();
        ((Func<CatalogTable>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "vectors", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, columns, [sql_storage_engine.Exposures.Catalog.CatalogCheckConstraintExposure.CreateCatalogCheckConstraint("CK_vector","[v] IS NULL")]))).Should().Throw<ArgumentException>();
        ((Func<CatalogTable>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 2 }, "masked", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 2 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "secret", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, maskingFunction: "default()"), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "derived", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, computedExpression: "secret + 1")]))).Should().Throw<ArgumentException>();
        ((Func<CatalogTable>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 3 }, "defaults", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 3 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "source", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "copy", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, defaultExpression: "source")]))).Should().Throw<ArgumentException>();
        ((Func<CatalogTable>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 4 }, "cycles", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 4 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "a", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, computedExpression: "b + 1"), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "b", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, computedExpression: "a + 1")]))).Should().Throw<ArgumentException>();
    }

    [Test]
    public async Task ComputedColumnsFollowDependenciesRatherThanPhysicalColumnOrder()
    {
        var path = TempDatabase("computed-order");
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var table = await engine.CreateCatalogTableAsync("computed_order",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "twice", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, computedExpression: "base_value * 2"), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "plus_one", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, computedExpression: "twice + 1"), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "base_value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)]);
            var storage = await engine.OpenTableAsync(table.Id);
            var rowId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(4)]));
            (await storage.GetAsync(rowId))!.Row.Values.Should().Equal(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(8), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(9), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(4));
            await storage.UpdateAsync(rowId,RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 2, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7) }]));
            (await storage.GetAsync(rowId))!.Row.Values.Should().Equal(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(14), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(15), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task SourceAwareConversionsPreserveMoneyAndLegacyDateTimeRules()
    {
        sql_storage_engine.Exposures.Rows.SqlConversionExposure.ConvertTo(sqlTypes:[sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeMoney()],sqlValue:sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(-10.5m)).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(-11));
        sql_storage_engine.Exposures.Rows.SqlConversionExposure.ConvertTo(sqlTypes:[sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(10,2)],sqlValue:sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(-10.5m)).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(-10));
        sql_storage_engine.Exposures.Rows.SqlConversionExposure.ConvertTo(sqlTypes:[sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDateTime()],sqlValue:sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(1900, 1, 2, 12, 0, 0))).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2));
        var path = TempDatabase("source-conversion");
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var table = await engine.CreateCatalogTableAsync("source_conversion",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "money_as_int", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, computedExpression: "amount"), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "numeric_as_int", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, computedExpression: "exact"), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "amount", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeMoney(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 }, "exact", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(10,2), false)]);
            var storage = await engine.OpenTableAsync(table.Id);
            var rowId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(-10.5m), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(-10.5m)]));
            (await storage.GetAsync(rowId))!.Row.Values.Should().Equal(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(-11), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(-10), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(-10.5000m), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(-10.50m));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public void SqlServerTableColumnCapacityRulesAreEnforced()
    {
        var ordinary = Enumerable.Range(1, 1_025).Select(index => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = (ulong)index }, $"c{index}", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), true)).ToArray();
        ((Func<CatalogTable>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "too_wide", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, ordinary))).Should().Throw<ArgumentException>();
        var wide = new List<CatalogColumn>
        {
            sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false),
            sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "all_sparse", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(), true, isColumnSet: true)
        };
        wide.AddRange(Enumerable.Range(3, 1_023).Select(index => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = (ulong)index }, $"s{index}", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), true, isSparse: true)));
        sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 2 }, "wide", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 2 }, wide).Columns.Should().HaveCount(1_025);
    }

    private static string TempDatabase(string purpose) => Path.Combine(Path.GetTempPath(), $"sql-{purpose}-{Guid.NewGuid():N}.db");
    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
        if (File.Exists(path + ".wal"))
            File.Delete(path + ".wal");
    }
}