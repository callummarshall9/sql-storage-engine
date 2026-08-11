using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Tables;
using System.Buffers.Binary;
using System.Numerics;

namespace sql_storage_engine.UnitTests;

public sealed class SqlServerGapClosureTests
{
    [Test]
    public void HierarchyIdSupportsFullLabelsNavigationOrderingAndBinaryRoundTrip()
    {
        var value = SqlHierarchyId.Parse("/123456789012345678901234567890/-4.7/9/");

        SqlHierarchyId.Deserialize(value.Serialize()).Should().Be(value);
        value.GetLevel().Should().Be(3);
        value.GetAncestor(2).Should().Be(SqlHierarchyId.Parse("/123456789012345678901234567890/"));
        value.GetReparentedValue(SqlHierarchyId.Parse("/123456789012345678901234567890/"),
            SqlHierarchyId.Parse("/8/")).Should().Be(SqlHierarchyId.Parse("/8/-4.7/9/"));

        var root = SqlHierarchyId.Root;
        var left = SqlHierarchyId.Parse("/1/");
        var right = SqlHierarchyId.Parse("/2/");
        var middle = root.GetDescendant(left, right);
        middle.CompareTo(left).Should().BePositive();
        middle.CompareTo(right).Should().BeNegative();
        ((Action)(() => SqlHierarchyId.Parse("/01/"))).Should().Throw<FormatException>();
    }

    [Test]
    public void SpatialValuesSupportStructuredShapesWkbGmlAndTopology()
    {
        var polygon = new SqlSpatial(SqlSpatialKind.Geometry, 0,
            "POLYGON ((0 0,10 0,10 10,0 10,0 0))");
        var point = new SqlSpatial(SqlSpatialKind.Geometry, 0, "POINT (5 5)");
        var disjointLine = new SqlSpatial(SqlSpatialKind.Geometry, 0, "LINESTRING (20 0,30 0)");

        polygon.STIsValid().Should().BeTrue();
        polygon.STArea().Should().BeApproximately(100, 0.000001);
        polygon.STContains(point).Should().BeTrue();
        polygon.STIntersects(disjointLine).Should().BeFalse();
        SqlSpatial.FromWellKnownBinary(SqlSpatialKind.Geometry, polygon.STAsBinary())
            .STEquals(polygon).Should().BeTrue();
        polygon.STEquals(new SqlSpatial(SqlSpatialKind.Geometry, 0,
            "POLYGON ((10 0,10 10,0 10,0 0,5 0,10 0))")).Should().BeTrue();
        point.STEquals(new SqlSpatial(SqlSpatialKind.Geometry, 0, "POINT Z (5 5 99)")).Should().BeTrue();
        point.STDistance(new SqlSpatial(SqlSpatialKind.Geometry, 1, "POINT (5 5)")).Should().BeNull();
        point.STIntersects(new SqlSpatial(SqlSpatialKind.Geometry, 1, "POINT (5 5)")).Should().BeNull();
        SqlSpatial.Deserialize(polygon.Serialize(), SqlSpatialKind.Geometry).Should().Be(polygon);
        polygon.AsGml().Should().Contain("Polygon");

        var globe = new SqlSpatial(SqlSpatialKind.Geography, 4326, "FULLGLOBE");
        globe.STContains(new SqlSpatial(SqlSpatialKind.Geography, 4326, "POINT (0 0)")).Should().BeTrue();
    }

    [Test]
    public async Task DefaultsIdentityComputedChecksSparseAndGeneratedColumnsExecuteDurably()
    {
        var path = TempDatabase("generated");
        try
        {
            TableId tableId;
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var table = await engine.CreateTableAsync("orders", [
                    new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false, identity: new CatalogIdentity(10, 2)),
                    new CatalogColumn(new ColumnId(2), "quantity", SqlType.Int, false, defaultExpression: "2"),
                    new CatalogColumn(new ColumnId(3), "total", SqlType.Int, false,
                        computedExpression: "[quantity] * 3", isComputedPersisted: true),
                    new CatalogColumn(new ColumnId(4), "rv", SqlType.RowVersion, false),
                    new CatalogColumn(new ColumnId(5), "tag", SqlType.NVarChar(20), true, isSparse: true),
                    new CatalogColumn(new ColumnId(6), "sparse_values", SqlType.Xml, true, isColumnSet: true),
                    new CatalogColumn(new ColumnId(7), "period_start", SqlType.DateTime2(), false,
                        generatedAlways: CatalogGeneratedAlwaysKind.RowStart, isHidden: true),
                    new CatalogColumn(new ColumnId(8), "sequence", SqlType.BigInt, false,
                        generatedAlways: CatalogGeneratedAlwaysKind.SequenceNumberStart, isHidden: true)
                ], [new CatalogCheckConstraint("CK_orders_quantity", "quantity > 0")]);
                tableId = table.Id;
                var storage = await engine.OpenTableAsync(table.Id);
                var firstId = await storage.InsertAsync(new Row([
                    SqlValue.Default, SqlValue.Default, SqlValue.Default, SqlValue.Null,
                    SqlValue.Text("priority"), SqlValue.Null, SqlValue.Null, SqlValue.Null]));
                var first = (await storage.GetAsync(firstId))!.Row;
                first.Values[0].Should().Be(SqlValue.Integer(10));
                first.Values[1].Should().Be(SqlValue.Integer(2));
                first.Values[2].Should().Be(SqlValue.Integer(6));
                first.Values[3].Should().BeOfType<BinarySqlValue>();
                ((TextSqlValue)first.Values[5]).Value.Should().Contain("<tag>priority</tag>");
                first.Values[6].Should().BeOfType<DateTimeSqlValue>();
                first.Values[7].Should().Be(SqlValue.Integer(1));
                await ((Func<Task>)(async () => await storage.InsertAsync(new Row([
                    SqlValue.Default, SqlValue.Integer(-1), SqlValue.Default, SqlValue.Null,
                    SqlValue.Null, SqlValue.Null, SqlValue.Null, SqlValue.Null]))))
                    .Should().ThrowAsync<ArgumentException>();
            }

            await using (var reopened = await StorageEngine.OpenAsync(path))
            {
                var storage = await reopened.OpenTableAsync(tableId);
                var nextId = await storage.InsertAsync(new Row([
                    SqlValue.Default, SqlValue.Integer(4), SqlValue.Default, SqlValue.Null,
                    SqlValue.Null, SqlValue.Null, SqlValue.Null, SqlValue.Null]));
                var next = (await storage.GetAsync(nextId))!.Row;
                next.Values[0].Should().Be(SqlValue.Integer(14), "failed SQL inserts consume an identity value");
                next.Values[2].Should().Be(SqlValue.Integer(12));
                next.Values[7].Should().Be(SqlValue.Integer(3), "generated sequence values are also consumed by failed inserts");
            }
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task Decimal38IdentityIsNotLimitedByClrInt64()
    {
        var path = TempDatabase("decimal-identity");
        var seed = BigInteger.Parse("12345678901234567890123456789012345670");
        try
        {
            TableId tableId;
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var table = await engine.CreateTableAsync("large_ids", [
                    new CatalogColumn(new ColumnId(1), "id", SqlType.Decimal(38, 0), false,
                        identity: new CatalogIdentity(seed, BigInteger.One))
                ]);
                tableId = table.Id;
                var rowId = await (await engine.OpenTableAsync(table.Id)).InsertAsync(new Row([SqlValue.Default]));
                (await (await engine.OpenTableAsync(table.Id)).GetAsync(rowId))!.Row.Values[0]
                    .Should().Be(SqlValue.Decimal(seed, 0));
            }
            await using (var reopened = await StorageEngine.OpenAsync(path))
            {
                var storage = await reopened.OpenTableAsync(tableId);
                var rowId = await storage.InsertAsync(new Row([SqlValue.Default]));
                (await storage.GetAsync(rowId))!.Row.Values[0].Should().Be(SqlValue.Decimal(seed + 1, 0));
            }
            ((Func<CatalogColumn>)(() => new CatalogColumn(new ColumnId(1), "bad", SqlType.Decimal(38, 1),
                false, identity: new CatalogIdentity()))).Should().Throw<ArgumentException>();
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task AlwaysEncryptedColumnsRoundTripAndRespectIndexRestrictions()
    {
        var path = TempDatabase("encrypted");
        SqlColumnEncryption.RegisterKey("det_key", Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        SqlColumnEncryption.RegisterKey("rnd_key", Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        try
        {
            TableId tableId; RowId firstId;
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var table = await engine.CreateTableAsync("secrets", [
                    new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
                    new CatalogColumn(new ColumnId(2), "secret", SqlType.NVarChar(40, "Latin1_General_100_BIN2"), false,
                        encryption: new CatalogColumnEncryption("det_key", CatalogEncryptionType.Deterministic)),
                    new CatalogColumn(new ColumnId(3), "nonce", SqlType.Int, false,
                        encryption: new CatalogColumnEncryption("rnd_key", CatalogEncryptionType.Randomized))
                ]);
                tableId = table.Id;
                await engine.CreateIndexAsync("PK_secrets", table.Id, true,
                    [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)],
                    new CatalogBTreeIndexOptions(CatalogIndexStorageKind.Clustered, isPrimaryKey: true));
                var secretIndex = await engine.CreateIndexAsync("IX_secret", table.Id, false,
                    [new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.First)]);
                var storage = await engine.OpenTableAsync(table.Id);
                firstId = await storage.InsertAsync(new Row([SqlValue.Integer(1), SqlValue.Text("classified"), SqlValue.Integer(7)]));
                await storage.InsertAsync(new Row([SqlValue.Integer(2), SqlValue.Text("classified"), SqlValue.Integer(7)]));
                (await (await engine.OpenIndexAsync(secretIndex.Id)).FindAsync([SqlValue.Text("classified")]))
                    .Should().HaveCount(2);
                await ((Func<Task>)(async () => await engine.CreateIndexAsync("IX_nonce", table.Id, false,
                    [new CatalogIndexedColumn(new ColumnId(3), SortDirection.Ascending, NullSortOrder.First)])))
                    .Should().ThrowAsync<ArgumentException>();
            }

            await using (var reopened = await StorageEngine.OpenAsync(path))
            {
                var row = await (await reopened.OpenTableAsync(tableId)).GetAsync(firstId);
                row!.Row.Values.Should().Equal(SqlValue.Integer(1), SqlValue.Text("classified"), SqlValue.Integer(7));
                reopened.Catalog.Tables.Single().Columns[1].Encryption!.EncryptionType
                    .Should().Be(CatalogEncryptionType.Deterministic);
            }
        }
        finally
        {
            SqlColumnEncryption.Unregister("det_key"); SqlColumnEncryption.Unregister("rnd_key"); DeleteDatabase(path);
        }
    }

    [Test]
    public void ExtendedColumnFacetsPersistAndMaskingFunctionsExecute()
    {
        CatalogColumn[] columns = [
            new CatalogColumn(new ColumnId(1), "stream_id", SqlType.UniqueIdentifier, false,
                defaultExpression: "newid()", isRowGuidCol: true),
            new CatalogColumn(new ColumnId(2), "payload", SqlType.VarBinaryMax, true, isFileStream: true),
            new CatalogColumn(new ColumnId(3), "nickname", SqlType.NVarChar(30), true,
                maskingFunction: "partial(1,'XXXX',1)"),
            new CatalogColumn(new ColumnId(4), "sparse_values", SqlType.Xml, true, isColumnSet: true),
            new CatalogColumn(new ColumnId(5), "period_end", SqlType.DateTime2(), false,
                generatedAlways: CatalogGeneratedAlwaysKind.RowEnd, isHidden: true)
        ];
        var table = new CatalogTable(new TableId(1), "files", 1, new PageId(1), columns);
        var catalog = new CatalogDefinition([table], [
            new CatalogIndex(new IndexId(1), "UQ_files_stream_id", table.Id, new PageId(2), true, [
                new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)
            ])
        ]);

        var decoded = CatalogCodec.Decode(CatalogCodec.Encode(catalog));
        decoded.Tables.Single().Columns.Should().BeEquivalentTo(columns);
        SqlDataMasking.Apply(columns[2], SqlValue.Text("Callum")).Should().Be(SqlValue.Text("CXXXXm"));
    }

    [Test]
    public async Task JsonAndXmlIndexesSupportAdvancedPathsAndPersistTheirKinds()
    {
        var path = TempDatabase("document-index");
        try
        {
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var table = await engine.CreateTableAsync("documents", [
                    new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
                    new CatalogColumn(new ColumnId(2), "body", SqlType.Json, false),
                    new CatalogColumn(new ColumnId(3), "xml_body", SqlType.Xml, false)
                ]);
                await engine.CreateIndexAsync("PK_documents", table.Id, true,
                    [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)],
                    new CatalogBTreeIndexOptions(CatalogIndexStorageKind.Clustered, isPrimaryKey: true));
                var jsonValue = SqlValue.Text("{\"items\":[0,1,2,3],\"odd.name\":9}");
                var xmlValue = SqlValue.Text("<root xmlns:d=\"urn:documents\"><item id=\"1\"/><item id=\"2\"/>" +
                                             "<meta>x</meta><!--note--><?audit ok?>" +
                                             "<d:item d:code=\"N2\">namespaced</d:item></root>");
                var storage = await engine.OpenTableAsync(table.Id);
                var rowId = await storage.InsertAsync(new Row([SqlValue.Integer(1), jsonValue, xmlValue]));
                var json = await engine.CreateSpecializedIndexAsync("IX_json", table.Id,
                    new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.First),
                    CatalogSpecializedIndexOptions.Json());
                var xml = await engine.CreateSpecializedIndexAsync("IX_xml", table.Id,
                    new CatalogIndexedColumn(new ColumnId(3), SortDirection.Ascending, NullSortOrder.First),
                    CatalogSpecializedIndexOptions.Xml(CatalogXmlIndexKind.Selective,
                        new Dictionary<string, string> { ["d"] = "urn:documents" },
                        "/root/item", "/root/item/@id", "/root/meta", "/root/d:item/@d:code"));
                var primaryXml = await engine.CreateSpecializedIndexAsync("IX_xml_primary", table.Id,
                    new CatalogIndexedColumn(new ColumnId(3), SortDirection.Ascending, NullSortOrder.First),
                    CatalogSpecializedIndexOptions.Xml(CatalogXmlIndexKind.Primary,
                        new Dictionary<string, string> { ["d"] = "urn:documents" }));
                var jsonIndex = await engine.OpenIndexAsync(json.Id);
                var xmlIndex = await engine.OpenIndexAsync(xml.Id);
                var primaryXmlIndex = await engine.OpenIndexAsync(primaryXml.Id);
                (await jsonIndex.FindAsync([jsonValue])).Should().Equal(rowId);
                (await xmlIndex.FindAsync([xmlValue])).Should().Equal(rowId);
                (await jsonIndex.FindJsonPathAsync("$.items[1 to 2]", SqlValue.Integer(1))).Should().Equal(rowId);
                (await jsonIndex.FindJsonPathAsync("$.\"odd.name\"", SqlValue.Integer(9))).Should().Equal(rowId);
                (await jsonIndex.FindJsonPathExistsAsync("$.items[1 to 2]")).Should().Equal(rowId);
                (await jsonIndex.FindJsonPathAsync("$.items[*]", SqlValue.Integer(2))).Should().Equal(rowId);
                (await jsonIndex.FindJsonPathAsync("$.items[last, 0]", SqlValue.Integer(3))).Should().Equal(rowId);
                (await xmlIndex.FindXmlPathAsync("/root/meta", "x")).Should().Equal(rowId);
                (await xmlIndex.FindXmlPathAsync("/root/item")).Should().Equal(rowId);
                (await xmlIndex.FindXmlPathAsync("/root/item/@id", "2")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/item/@id", "2")).Should().Equal(rowId);
                (await xmlIndex.FindXmlPathAsync("/root/d:item/@d:code", "N2")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/d:item/@d:code", "N2")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/d:item/text()", "namespaced")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/comment()", "note")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/processing-instruction()", "ok")).Should().Equal(rowId);

                var tooDeep = string.Concat(Enumerable.Repeat("<n>", 129)) + "x" +
                              string.Concat(Enumerable.Repeat("</n>", 129));
                await ((Func<Task>)(async () => await storage.UpdateAsync(rowId,
                        new RowUpdate([new ColumnUpdate(2, SqlValue.Text(tooDeep))]))))
                    .Should().ThrowAsync<TableMutationException>();
                (await xmlIndex.FindXmlPathAsync("/root/meta", "x")).Should().Equal(rowId);

                await storage.UpdateAsync(rowId, new RowUpdate([
                    new ColumnUpdate(1, SqlValue.Text("{\"items\":[5,6],\"odd.name\":10}")),
                    new ColumnUpdate(2, SqlValue.Text("<root><item>three</item><meta>y</meta></root>"))
                ]));
                (await jsonIndex.FindJsonPathAsync("$.items[1 to 2]", SqlValue.Integer(1))).Should().BeEmpty();
                (await jsonIndex.FindJsonPathAsync("$.items[1 to 2]", SqlValue.Integer(6))).Should().Equal(rowId);
                (await xmlIndex.FindXmlPathAsync("/root/meta", "x")).Should().BeEmpty();
                (await xmlIndex.FindXmlPathAsync("/root/meta", "y")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/item/@id", "2")).Should().BeEmpty();
                (await primaryXmlIndex.FindXmlPathAsync("/root/item", "three")).Should().Equal(rowId);
                (await primaryXmlIndex.FindXmlPathAsync("/root/item/text()", "three")).Should().Equal(rowId);
            }

            await using var reopened = await StorageEngine.OpenAsync(path);
            var persistedXml = reopened.Catalog.Indexes.Single(index => index.Method == CatalogIndexMethod.Xml &&
                index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Selective);
            persistedXml.SpecializedOptions!.XmlIndexKind.Should().Be(CatalogXmlIndexKind.Selective);
            persistedXml.SpecializedOptions.XmlNamespaces.Should().Contain("d", "urn:documents");
            var reopenedXml = await reopened.OpenIndexAsync(persistedXml.Id);
            var persistedRows = await reopenedXml.FindXmlPathAsync("/root/meta", "y");
            persistedRows.Should().ContainSingle();
            var persistedPrimaryXml = reopened.Catalog.Indexes.Single(index => index.Method == CatalogIndexMethod.Xml &&
                index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Primary);
            var reopenedPrimaryXml = await reopened.OpenIndexAsync(persistedPrimaryXml.Id);
            (await reopenedPrimaryXml.FindXmlPathAsync("/root/item", "three"))
                .Should().ContainSingle();
            var persistedJson = reopened.Catalog.Indexes.Single(index => index.Method == CatalogIndexMethod.Json);
            var reopenedJson = await reopened.OpenIndexAsync(persistedJson.Id);
            (await reopenedJson.FindJsonPathAsync(
                "$.items[1 to 2]", SqlValue.Integer(6))).Should().ContainSingle();
            var persistedTable = reopened.Catalog.Tables.Single(table => table.Name == "documents");
            (await (await reopened.OpenTableAsync(persistedTable.Id)).DeleteAsync(persistedRows.Single())).Deleted
                .Should().BeTrue();
            (await reopenedXml.FindXmlPathAsync("/root/meta", "y")).Should().BeEmpty();
            (await reopenedPrimaryXml.FindXmlPathAsync("/root/item", "three")).Should().BeEmpty();
            (await reopenedJson.FindJsonPathAsync("$.items[1 to 2]", SqlValue.Integer(6))).Should().BeEmpty();
            ((Func<CatalogSpecializedIndexOptions>)(() => CatalogSpecializedIndexOptions.Json("$.items[2 to 1]")))
                .Should().Throw<ArgumentException>();
            ((Func<CatalogSpecializedIndexOptions>)(() => CatalogSpecializedIndexOptions.Xml(
                CatalogXmlIndexKind.Selective, "root/["))).Should().Throw<ArgumentException>();
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public void JsonPathsSupportAnsiListsEscapesEmptyKeysAndDepthLimits()
    {
        var table = new CatalogTable(new TableId(1), "json_paths", 1, new PageId(1), [
            new CatalogColumn(new ColumnId(1), "body", SqlType.Json, false)
        ]);
        var index = new CatalogIndex(new IndexId(1), "IX_json_paths", table.Id, new PageId(2), false, [
            new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)
        ], CatalogSpecializedIndexOptions.Json("$.items[last, 0]", "$.\"odd\\u002ename\"", "$.\"\""));
        var entries = CatalogIndexKey.EncodeEntries(new Row([SqlValue.Text(
            "{\"items\":[0,1,2],\"odd.name\":9,\"\":true}")]), table, index);

        entries.Should().Contain(CatalogIndexKey.EncodeJsonPathValue(index,
            "$.items[last, 0]", SqlValue.Integer(2)));
        entries.Should().Contain(CatalogIndexKey.EncodeJsonPathValue(index,
            "$.items[last, 0]", SqlValue.Integer(0)));
        entries.Should().Contain(CatalogIndexKey.EncodeJsonPathValue(index,
            "$.\"odd\\u002ename\"", SqlValue.Integer(9)));
        entries.Should().Contain(CatalogIndexKey.EncodeJsonPathValue(index, "$.\"\"", SqlValue.Boolean(true)));

        var recursive = new CatalogIndex(new IndexId(2), "IX_json_recursive", table.Id, new PageId(3), false, [
            new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)
        ], CatalogSpecializedIndexOptions.Json());
        var recursiveEntries = CatalogIndexKey.EncodeEntries(new Row([SqlValue.Text(
            "{\"items\":[0,1,2],\"odd.name\":9}")]), table, recursive);
        recursiveEntries.Should().Contain(CatalogIndexKey.EncodeJsonPathValue(recursive,
            "$.items[1]", SqlValue.Integer(1)));
        recursiveEntries.Should().Contain(CatalogIndexKey.EncodeJsonPathExists(recursive, "$.\"odd.name\""));

        var tooDeep = "$" + string.Concat(Enumerable.Repeat(".a", 129));
        ((Func<CatalogSpecializedIndexOptions>)(() => CatalogSpecializedIndexOptions.Json(tooDeep)))
            .Should().Throw<ArgumentException>();
    }

    [Test]
    public void NativeJsonUsesSqlServerDocumentLimitsInsteadOfRuntimeDefaults()
    {
        var depth100 = string.Concat(Enumerable.Repeat("{\"a\":", 100)) + "0" + new string('}', 100);
        SqlType.Json.Validate(SqlValue.Text(depth100), "body");
        var definition = new TableDefinition([
            new ColumnDefinition(new ColumnId(1), "body", SqlType.Json, false)
        ]);
        RowCodec.Decode(RowCodec.Encode(new Row([SqlValue.Text(depth100)]), definition), definition)
            .Values.Should().ContainSingle();

        var depth129 = string.Concat(Enumerable.Repeat("{\"a\":", 129)) + "0" + new string('}', 129);
        ((Action)(() => SqlType.Json.Validate(SqlValue.Text(depth129), "body")))
            .Should().Throw<ArgumentException>();
        var oversizedKey = "{\"" + new string('k', 7_999) + "\":0}";
        ((Action)(() => SqlType.Json.Validate(SqlValue.Text(oversizedKey), "body")))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task DatabaseDefaultCollationIsResolvedAtDdlAndPersists()
    {
        var path = TempDatabase("collation");
        const string collation = "Latin1_General_100_CI_AI_SC_UTF8";
        try
        {
            await using (var engine = await StorageEngine.CreateAsync(path,
                new StorageEngineOptions { DefaultCollation = collation }))
            {
                var table = await engine.CreateTableAsync("names", [
                    new CatalogColumn(new ColumnId(1), "name", SqlType.VarChar(100), false)
                ]);
                table.Columns.Single().Type.Collation.Should().Be(collation);
            }
            await using var reopened = await StorageEngine.OpenAsync(path);
            reopened.Catalog.DefaultCollation.Should().Be(collation);
            reopened.Catalog.Tables.Single().Columns.Single().Type.Collation.Should().Be(collation);
            ((Func<SqlCollation>)(() => SqlCollation.Parse("Unknown_Collation_CI_AS")))
                .Should().Throw<ArgumentException>();
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public void CurrentVectorJsonAndSparseRestrictionsAreEnforced()
    {
        ((Func<SqlType>)(() => SqlType.Vector(1_999, SqlVectorBaseType.Float32))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<SqlType>)(() => SqlType.Vector(1_999, SqlVectorBaseType.Float16))).Should().Throw<ArgumentOutOfRangeException>();
        SqlType.Vector(1_998, SqlVectorBaseType.Float16).VectorDimensions.Should().Be(1_998);

        var json = new ColumnDefinition(new ColumnId(1), "document", SqlType.Json, false);
        json.Validate(SqlValue.Text("{}"));
        json.Validate(SqlValue.Text("[]"));
        foreach (var scalar in new[] { "true", "null", "42", "\"text\"" })
            ((Action)(() => json.Validate(SqlValue.Text(scalar)))).Should().Throw<ArgumentException>();

        ((Func<CatalogColumn>)(() => new CatalogColumn(new ColumnId(1), "value", SqlType.Int, true,
            defaultExpression: "0", isSparse: true))).Should().Throw<ArgumentException>();
        ((Func<CatalogColumn>)(() => new CatalogColumn(new ColumnId(1), "value", SqlType.Int, true,
            identity: new CatalogIdentity(), isSparse: true))).Should().Throw<ArgumentException>();
        ((Func<CatalogColumn>)(() => new CatalogColumn(new ColumnId(1), "value", SqlType.Int, true,
            computedExpression: "1", isSparse: true))).Should().Throw<ArgumentException>();

        var emptyColumnSetTable = new CatalogTable(new TableId(1), "future_sparse", 1, new PageId(1), [
            new CatalogColumn(new ColumnId(1), "values", SqlType.Xml, true, isColumnSet: true)
        ]);
        emptyColumnSetTable.Columns.Should().ContainSingle(column => column.IsColumnSet);
        ((Func<CatalogTableType>)(() => new CatalogTableType("dbo", "bad_sparse_type", [
            new CatalogColumn(new ColumnId(1), "value", SqlType.Int, true, isSparse: true)
        ]))).Should().Throw<ArgumentException>();
    }

    [Test]
    public async Task XmlColumnSetIsUpdatableProjectedAndNotAuthoritativeStorage()
    {
        var path = TempDatabase("column-set");
        try
        {
            TableId tableId; RowId rowId;
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var table = await engine.CreateTableAsync("properties", [
                    new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
                    new CatalogColumn(new ColumnId(2), "quantity", SqlType.Int, true, isSparse: true),
                    new CatalogColumn(new ColumnId(3), "label", SqlType.NVarChar(20), true, isSparse: true),
                    new CatalogColumn(new ColumnId(4), "properties", SqlType.Xml, true, isColumnSet: true)
                ]);
                tableId = table.Id;
                var storage = await engine.OpenTableAsync(table.Id);
                rowId = await storage.InsertAsync(new Row([
                    SqlValue.Integer(1), SqlValue.Null, SqlValue.Null,
                    SqlValue.Text("<quantity>7</quantity><label>first</label>")
                ]));
                var inserted = (await storage.GetAsync(rowId))!.Row;
                inserted.Values[1].Should().Be(SqlValue.Integer(7));
                inserted.Values[2].Should().Be(SqlValue.Text("first"));
                ((TextSqlValue)inserted.Values[3]).Value.Should().Be("<quantity>7</quantity><label>first</label>");

                await storage.UpdateAsync(rowId, new RowUpdate([
                    new ColumnUpdate(3, SqlValue.Text("<label>second</label>"))
                ]));
                var updated = (await storage.GetAsync(rowId))!.Row;
                updated.Values[1].Should().Be(SqlValue.Null);
                updated.Values[2].Should().Be(SqlValue.Text("second"));
                ((TextSqlValue)updated.Values[3]).Value.Should().Be("<label>second</label>");
            }

            await using var reopened = await StorageEngine.OpenAsync(path);
            var durable = (await (await reopened.OpenTableAsync(tableId)).GetAsync(rowId))!.Row;
            durable.Values[1].Should().Be(SqlValue.Null);
            durable.Values[2].Should().Be(SqlValue.Text("second"));
            ((TextSqlValue)durable.Values[3]).Value.Should().Be("<label>second</label>");
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task DynamicDataMaskingValidatesFunctionsAndHonorsReadPermissionContext()
    {
        ((Func<CatalogColumn>)(() => new CatalogColumn(new ColumnId(1), "bad", SqlType.Int, true,
            maskingFunction: "email()"))).Should().Throw<ArgumentException>();
        ((Func<CatalogColumn>)(() => new CatalogColumn(new ColumnId(1), "bad", SqlType.NVarChar(10), true,
            maskingFunction: "random(10,1)"))).Should().Throw<ArgumentException>();
        var path = TempDatabase("masking");
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var table = await engine.CreateTableAsync("members", [
                new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
                new CatalogColumn(new ColumnId(2), "name", SqlType.NVarChar(20), false,
                    maskingFunction: "partial(1,\"XXXX\",1)"),
                new CatalogColumn(new ColumnId(3), "email", SqlType.VarChar(100), false,
                    maskingFunction: "email()")
            ]);
            var storage = await engine.OpenTableAsync(table.Id);
            var rowId = await storage.InsertAsync(new Row([
                SqlValue.Integer(1), SqlValue.Text("Callum"), SqlValue.Text("callum@example.test")
            ]));

            (await storage.GetAsync(rowId))!.Row.Values[1].Should().Be(SqlValue.Text("Callum"));
            var masked = (await storage.GetAsync(rowId, StorageReadOptions.Unprivileged))!.Row;
            masked.Values[1].Should().Be(SqlValue.Text("CXXXXm"));
            masked.Values[2].Should().Be(SqlValue.Text("cXXX@XXXX.com"));
            var columnGrant = new StorageReadOptions(unmaskedColumns: [new ColumnId(2)]);
            var partial = (await storage.GetAsync(rowId, columnGrant))!.Row;
            partial.Values[1].Should().Be(SqlValue.Text("Callum"));
            partial.Values[2].Should().Be(SqlValue.Text("cXXX@XXXX.com"));
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public void DecimalExpressionsUseSqlScaleRulesAndLosslessExactComparison()
    {
        ((DecimalSqlValue)SqlExpressions.Evaluate("1.20 + 3.456")).Value
            .Should().Be(new SqlDecimal(4_656, 3));
        ((DecimalSqlValue)SqlExpressions.Evaluate("1.0 / 3.0")).Value
            .Should().Be(new SqlDecimal(333_333, 6));
        SqlExpressions.Evaluate("9007199254740993 = 9007199254740992.0")
            .Should().Be(SqlValue.Boolean(false));
        SqlExpressions.Evaluate("1 + '2'").Should().Be(SqlValue.Integer(3));
        ((Func<SqlValue>)(() => SqlExpressions.Evaluate("1 + 'not-a-number'")))
            .Should().Throw<ArgumentException>();
    }

    [Test]
    public void LegacyBinAndBin2CollationsHaveDistinctUnicodeOrdering()
    {
        var legacy = SqlCollation.Parse("Latin1_General_100_BIN");
        var modern = SqlCollation.Parse("Latin1_General_100_BIN2");
        const string byteFirst = "A\u0100";
        const string codePointFirst = "A\u0002";

        legacy.GetSortKey(byteFirst, unicode: true).AsSpan()
            .SequenceCompareTo(legacy.GetSortKey(codePointFirst, unicode: true)).Should().BeNegative();
        modern.GetSortKey(byteFirst, unicode: true).AsSpan()
            .SequenceCompareTo(modern.GetSortKey(codePointFirst, unicode: true)).Should().BePositive();
    }

    [Test]
    public void SparseNullsOmitFixedSlotsAndVariableDirectoryEntries()
    {
        var regular = new TableDefinition(Enumerable.Range(1, 8).Select(index =>
            new ColumnDefinition(new ColumnId((ulong)index), $"c{index}", SqlType.Int, true)));
        var sparse = new TableDefinition(Enumerable.Range(1, 8).Select(index =>
            new ColumnDefinition(new ColumnId((ulong)index), $"c{index}", SqlType.Int, true, isSparse: true)));
        var nullRow = new Row(Enumerable.Repeat(SqlValue.Null, 8));

        var regularBytes = RowCodec.Encode(nullRow, regular);
        var sparseBytes = RowCodec.Encode(nullRow, sparse);
        sparseBytes.Should().HaveCount(RowCodec.HeaderLength + 1);
        sparseBytes.Length.Should().BeLessThan(regularBytes.Length);
        RowCodec.Decode(sparseBytes, sparse).Values.Should().OnlyContain(value => value.IsNull);

        var populated = new Row([SqlValue.Integer(7), .. Enumerable.Repeat(SqlValue.Null, 7)]);
        RowCodec.Decode(RowCodec.Encode(populated, sparse), sparse).Values.Should().Equal(populated.Values);
    }

    [Test]
    public async Task IgnoreDuplicateKeySkipsRowsAndReturnsAWarning()
    {
        var path = TempDatabase("ignore-duplicate");
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var table = await engine.CreateTableAsync("codes", [
                new CatalogColumn(new ColumnId(1), "code", SqlType.Int, false),
                new CatalogColumn(new ColumnId(2), "description", SqlType.NVarChar(20), false)
            ]);
            await engine.CreateIndexAsync("UX_codes", table.Id, true,
                [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)],
                new CatalogBTreeIndexOptions(ignoreDuplicateKey: true));
            var storage = await engine.OpenTableAsync(table.Id);
            await storage.InsertAsync(new Row([SqlValue.Integer(7), SqlValue.Text("first")]));

            var skipped = await storage.TryInsertAsync(new Row([SqlValue.Integer(7), SqlValue.Text("second")]));
            skipped.Inserted.Should().BeFalse();
            skipped.RowId.Should().BeNull();
            skipped.Warnings.Should().ContainSingle().Which.Should().Contain("UX_codes");
            var rows = new List<StoredRow>();
            await foreach (var row in storage.ScanAsync()) rows.Add(row);
            rows.Should().ContainSingle();
            await ((Func<Task>)(async () => await storage.InsertAsync(
                new Row([SqlValue.Integer(7), SqlValue.Text("strict wrapper")]))))
                .Should().ThrowAsync<DuplicateKeyIgnoredException>();
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task VariableWidthIndexesEnforceActualRatherThanDeclaredKeyWidth()
    {
        var wideTable = new CatalogTable(new TableId(1), "wide_keys", 1, new PageId(1), [
            new CatalogColumn(new ColumnId(1), "value", SqlType.VarChar(8_000, "Latin1_General_100_BIN2"), false)
        ]);
        var wideIndex = new CatalogIndex(new IndexId(1), "IX_wide_keys", wideTable.Id, new PageId(2), false, [
            new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)
        ]);
        new CatalogDefinition([wideTable], [wideIndex]).Indexes.Should().ContainSingle();

        var fixedTable = new CatalogTable(new TableId(2), "fixed_keys", 1, new PageId(3), [
            new CatalogColumn(new ColumnId(1), "value", SqlType.Char(901, "Latin1_General_100_BIN2"), false)
        ]);
        var oversizedClustered = new CatalogIndex(new IndexId(2), "CX_fixed_keys", fixedTable.Id,
            new PageId(4), false, [
                new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)
            ], storageKind: CatalogIndexStorageKind.Clustered);
        ((Func<CatalogDefinition>)(() => new CatalogDefinition([fixedTable], [oversizedClustered])))
            .Should().Throw<ArgumentException>();

        var path = TempDatabase("runtime-key-width");
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var table = await engine.CreateTableAsync("keys", [
                new CatalogColumn(new ColumnId(1), "value", SqlType.VarChar(8_000, "Latin1_General_100_BIN2"), false)
            ]);
            await engine.CreateIndexAsync("IX_keys", table.Id, false, [
                new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)
            ]);
            var storage = await engine.OpenTableAsync(table.Id);
            await storage.InsertAsync(new Row([SqlValue.Text(new string('a', 1_700))]));
            await ((Func<Task>)(async () => await storage.InsertAsync(
                    new Row([SqlValue.Text(new string('b', 1_701))]))))
                .Should().ThrowAsync<TableMutationException>();
            var rows = new List<StoredRow>();
            await foreach (var row in storage.ScanAsync()) rows.Add(row);
            rows.Should().ContainSingle();
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public void SpecializedIndexesApplyTheirDistinctClusteringKeyAndSourceRules()
    {
        var keyColumns = Enumerable.Range(1, 16).Select(position =>
            new CatalogColumn(new ColumnId((ulong)position), $"k{position}", SqlType.TinyInt, false)).ToArray();
        var indexedKey = keyColumns.Select(column =>
            new CatalogIndexedColumn(column.Id, SortDirection.Ascending, NullSortOrder.First)).ToArray();
        var jsonTable = new CatalogTable(new TableId(1), "json_docs", 1, new PageId(1), [
            .. keyColumns,
            new CatalogColumn(new ColumnId(17), "body", SqlType.Json, false)
        ]);
        var primary = new CatalogIndex(new IndexId(1), "PK_json_docs", jsonTable.Id, new PageId(2), true,
            indexedKey, storageKind: CatalogIndexStorageKind.Clustered, isPrimaryKey: true);
        var json = new CatalogIndex(new IndexId(2), "IX_json_docs", jsonTable.Id, new PageId(3), false, [
            new CatalogIndexedColumn(new ColumnId(17), SortDirection.Ascending, NullSortOrder.First)
        ], CatalogSpecializedIndexOptions.Json());
        new CatalogDefinition([jsonTable], [primary, json]).Indexes.Should().HaveCount(2);

        var xmlTable = new CatalogTable(new TableId(2), "xml_docs", 1, new PageId(4), [
            .. keyColumns,
            new CatalogColumn(new ColumnId(17), "body", SqlType.Xml, false)
        ]);
        var xmlPrimary = new CatalogIndex(new IndexId(3), "PK_xml_docs", xmlTable.Id, new PageId(5), true,
            indexedKey, storageKind: CatalogIndexStorageKind.Clustered, isPrimaryKey: true);
        var xml = new CatalogIndex(new IndexId(4), "IX_xml_docs", xmlTable.Id, new PageId(6), false, [
            new CatalogIndexedColumn(new ColumnId(17), SortDirection.Ascending, NullSortOrder.First)
        ], CatalogSpecializedIndexOptions.Xml());
        ((Func<CatalogDefinition>)(() => new CatalogDefinition([xmlTable], [xmlPrimary, xml])))
            .Should().Throw<ArgumentException>();

        var computedJson = new CatalogTable(new TableId(3), "computed_json", 1, new PageId(7), [
            new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
            new CatalogColumn(new ColumnId(2), "body", SqlType.Json, true, computedExpression: "NULL")
        ]);
        var computedPrimary = new CatalogIndex(new IndexId(5), "PK_computed_json", computedJson.Id,
            new PageId(8), true, [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending,
                NullSortOrder.First)], storageKind: CatalogIndexStorageKind.Clustered, isPrimaryKey: true);
        var computedIndex = new CatalogIndex(new IndexId(6), "IX_computed_json", computedJson.Id,
            new PageId(9), false, [new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending,
                NullSortOrder.First)], CatalogSpecializedIndexOptions.Json());
        ((Func<CatalogDefinition>)(() => new CatalogDefinition([computedJson], [computedPrimary, computedIndex])))
            .Should().Throw<ArgumentException>();

        var computedXml = new CatalogTable(new TableId(4), "computed_xml", 1, new PageId(10), [
            new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
            new CatalogColumn(new ColumnId(2), "body", SqlType.Xml, true, computedExpression: "NULL")
        ]);
        var computedXmlPrimary = new CatalogIndex(new IndexId(7), "PK_computed_xml", computedXml.Id,
            new PageId(11), true, [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending,
                NullSortOrder.First)], storageKind: CatalogIndexStorageKind.Clustered, isPrimaryKey: true);
        CatalogIndex Selective(IndexId id, string name, PageId root) => new(id, name, computedXml.Id, root, false, [
            new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.First)
        ], CatalogSpecializedIndexOptions.Xml(CatalogXmlIndexKind.Selective, "/root/value"));
        ((Func<CatalogDefinition>)(() => new CatalogDefinition([computedXml],
            [computedXmlPrimary, Selective(new IndexId(8), "SXI_computed_xml", new PageId(12))])))
            .Should().Throw<ArgumentException>();

        var ordinaryXml = new CatalogTable(new TableId(5), "ordinary_xml", 1, new PageId(13), [
            new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
            new CatalogColumn(new ColumnId(2), "body", SqlType.Xml, true)
        ]);
        var ordinaryPrimary = new CatalogIndex(new IndexId(9), "PK_ordinary_xml", ordinaryXml.Id,
            new PageId(14), true, [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending,
                NullSortOrder.First)], storageKind: CatalogIndexStorageKind.Clustered, isPrimaryKey: true);
        CatalogIndex OrdinarySelective(IndexId id, string name, PageId root) => new(id, name, ordinaryXml.Id, root, false, [
            new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.First)
        ], CatalogSpecializedIndexOptions.Xml(CatalogXmlIndexKind.Selective, "/root/value"));
        ((Func<CatalogDefinition>)(() => new CatalogDefinition([ordinaryXml], [ordinaryPrimary,
            OrdinarySelective(new IndexId(10), "SXI_one", new PageId(15)),
            OrdinarySelective(new IndexId(11), "SXI_two", new PageId(16))])))
            .Should().Throw<ArgumentException>();
    }

    [Test]
    public void UserDefinedTableValuesEnforceUniqueIndexesAndIgnoreDuplicateKey()
    {
        CatalogColumn[] columns = [
            new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
            new CatalogColumn(new ColumnId(2), "label", SqlType.NVarChar(20), false)
        ];
        var key = new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First);
        var strict = new CatalogTableType("dbo", "StrictIds", columns, [
            new CatalogTableTypeIndex("UX_id", false, true, [key])
        ]);
        Row[] duplicates = [
            new([SqlValue.Integer(1), SqlValue.Text("first")]),
            new([SqlValue.Integer(1), SqlValue.Text("second")])
        ];
        ((Func<SqlValue>)(() => SqlValue.Table(strict, duplicates))).Should().Throw<ArgumentException>();

        var ignoring = new CatalogTableType("dbo", "IgnoringIds", columns, [
            new CatalogTableTypeIndex("UX_id", false, true, [key], ignoreDuplicateKey: true)
        ]);
        ((TableSqlValue)SqlValue.Table(ignoring, duplicates)).Rows.Should().ContainSingle();

        var computed = new CatalogTableType("dbo", "ComputedBatch", [
            new CatalogColumn(new ColumnId(1), "result", SqlType.Int, false, computedExpression: "intermediate + 1"),
            new CatalogColumn(new ColumnId(2), "intermediate", SqlType.Int, false, computedExpression: "source * 2"),
            new CatalogColumn(new ColumnId(3), "source", SqlType.Int, false)
        ]);
        ((TableSqlValue)SqlValue.Table(computed,
            [new Row([SqlValue.Default, SqlValue.Default, SqlValue.Integer(5)])])).Rows.Single().Values
            .Should().Equal(SqlValue.Integer(11), SqlValue.Integer(10), SqlValue.Integer(5));

        var wideIndex = new CatalogTableTypeIndex("IX_wide", false, false, [
            new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)
        ]);
        var wide = new CatalogTableType("dbo", "WideDiskBatch", [
            new CatalogColumn(new ColumnId(1), "value", SqlType.VarChar(8_000), false)
        ], [wideIndex]);
        ((Func<SqlValue>)(() => SqlValue.Table(wide,
            [new Row([SqlValue.Text(new string('x', 1_701))])]))).Should().Throw<ArgumentException>();

        var versioned = new CatalogTableType("dbo", "VersionedBatch", [
            new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
            new CatalogColumn(new ColumnId(2), "rv", SqlType.RowVersion, false)
        ]);
        var versions = ((TableSqlValue)SqlValue.Table(versioned, [
            new Row([SqlValue.Integer(1), SqlValue.Default]),
            new Row([SqlValue.Integer(2), SqlValue.Null])
        ])).Rows.Select(row => ((BinarySqlValue)row.Values[1]).Value.ToArray()).ToArray();
        BinaryPrimitives.ReadUInt64BigEndian(versions[0]).Should().Be(1);
        BinaryPrimitives.ReadUInt64BigEndian(versions[1]).Should().Be(2);
    }

    [Test]
    public void MemoryOptimizedTableTypesRejectUnsupportedTypesAndFacets()
    {
        var key = new CatalogTableTypeIndex("IX_id", false, false, [
            new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)
        ]);
        ((Func<CatalogTableType>)(() => new CatalogTableType("dbo", "VectorBatch", [
            new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
            new CatalogColumn(new ColumnId(2), "embedding", SqlType.Vector(3), true)
        ], [key], isMemoryOptimized: true))).Should().Throw<ArgumentException>();
        ((Func<CatalogTableType>)(() => new CatalogTableType("dbo", "IdentityBatch", [
            new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false, identity: new CatalogIdentity(10, 1))
        ], [key], isMemoryOptimized: true))).Should().Throw<ArgumentException>();
        var nullablePrimary = new CatalogTableTypeIndex("PK_id", true, true, [
            new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)
        ]);
        ((Func<CatalogTableType>)(() => new CatalogTableType("dbo", "NullableKeyBatch", [
            new CatalogColumn(new ColumnId(1), "id", SqlType.Int, true)
        ], [nullablePrimary], isMemoryOptimized: true))).Should().Throw<ArgumentException>();

        var oversizedOrdered = new CatalogTableTypeIndex("IX_wide", false, false, [
            new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)
        ]);
        ((Func<CatalogTableType>)(() => new CatalogTableType("dbo", "WideOrderedBatch", [
            new CatalogColumn(new ColumnId(1), "value", SqlType.VarChar(2_501), false)
        ], [oversizedOrdered], isMemoryOptimized: true))).Should().Throw<ArgumentException>();

        var hashPrimary = new CatalogTableTypeIndex("PK_wide", true, true, [
            new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)
        ], CatalogIndexStorageKind.Hash, bucketCount: 16);
        var wideHash = new CatalogTableType("dbo", "WideHashBatch", [
            new CatalogColumn(new ColumnId(1), "value", SqlType.VarChar(8_000), false)
        ], [hashPrimary], isMemoryOptimized: true);
        ((TableSqlValue)SqlValue.Table(wideHash,
            [new Row([SqlValue.Text(new string('x', 3_000))])])).Rows.Should().ContainSingle();

        ((Func<CatalogTableType>)(() => new CatalogTableType("dbo", "DefaultBatch", [
            new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false, defaultExpression: "1")
        ], [key], isMemoryOptimized: true))).Should().Throw<ArgumentException>();
        ((Func<CatalogTableTypeIndex>)(() => new CatalogTableTypeIndex("IX_too_many_buckets", false, false, [
            new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)
        ], CatalogIndexStorageKind.Hash, bucketCount: 1_073_741_825))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void CatalogDependencyChecksBindWholeIdentifiers()
    {
        CatalogColumn[] columns = [
            new CatalogColumn(new ColumnId(1), "v", SqlType.Vector(3), true),
            new CatalogColumn(new ColumnId(2), "value", SqlType.Int, false)
        ];
        var allowed = new CatalogTable(new TableId(1), "vectors", 1, new PageId(1), columns,
            [new CatalogCheckConstraint("CK_value", "'v' = 'v' AND value > 0")]);
        allowed.CheckConstraints.Should().ContainSingle();
        ((Func<CatalogTable>)(() => new CatalogTable(new TableId(1), "vectors", 1, new PageId(1), columns,
            [new CatalogCheckConstraint("CK_vector", "[v] IS NULL")])))
            .Should().Throw<ArgumentException>();

        ((Func<CatalogTable>)(() => new CatalogTable(new TableId(2), "masked", 1, new PageId(2), [
            new CatalogColumn(new ColumnId(1), "secret", SqlType.Int, false, maskingFunction: "default()"),
            new CatalogColumn(new ColumnId(2), "derived", SqlType.Int, false, computedExpression: "secret + 1")
        ]))).Should().Throw<ArgumentException>();

        ((Func<CatalogTable>)(() => new CatalogTable(new TableId(3), "defaults", 1, new PageId(3), [
            new CatalogColumn(new ColumnId(1), "source", SqlType.Int, false),
            new CatalogColumn(new ColumnId(2), "copy", SqlType.Int, false, defaultExpression: "source")
        ]))).Should().Throw<ArgumentException>();
        ((Func<CatalogTable>)(() => new CatalogTable(new TableId(4), "cycles", 1, new PageId(4), [
            new CatalogColumn(new ColumnId(1), "a", SqlType.Int, false, computedExpression: "b + 1"),
            new CatalogColumn(new ColumnId(2), "b", SqlType.Int, false, computedExpression: "a + 1")
        ]))).Should().Throw<ArgumentException>();
    }

    [Test]
    public async Task ComputedColumnsFollowDependenciesRatherThanPhysicalColumnOrder()
    {
        var path = TempDatabase("computed-order");
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var table = await engine.CreateTableAsync("computed_order", [
                new CatalogColumn(new ColumnId(1), "twice", SqlType.Int, false, computedExpression: "base_value * 2"),
                new CatalogColumn(new ColumnId(2), "plus_one", SqlType.Int, false, computedExpression: "twice + 1"),
                new CatalogColumn(new ColumnId(3), "base_value", SqlType.Int, false)
            ]);
            var storage = await engine.OpenTableAsync(table.Id);
            var rowId = await storage.InsertAsync(new Row([SqlValue.Default, SqlValue.Default, SqlValue.Integer(4)]));
            (await storage.GetAsync(rowId))!.Row.Values.Should().Equal(
                SqlValue.Integer(8), SqlValue.Integer(9), SqlValue.Integer(4));
            await storage.UpdateAsync(rowId, new RowUpdate([new ColumnUpdate(2, SqlValue.Integer(7))]));
            (await storage.GetAsync(rowId))!.Row.Values.Should().Equal(
                SqlValue.Integer(14), SqlValue.Integer(15), SqlValue.Integer(7));
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task SourceAwareConversionsPreserveMoneyAndLegacyDateTimeRules()
    {
        SqlConversion.ConvertTo(SqlType.Int, SqlType.Money, SqlValue.Decimal(-10.5m))
            .Should().Be(SqlValue.Integer(-11));
        SqlConversion.ConvertTo(SqlType.Int, SqlType.Decimal(10, 2), SqlValue.Decimal(-10.5m))
            .Should().Be(SqlValue.Integer(-10));
        SqlConversion.ConvertTo(SqlType.Int, SqlType.DateTime,
                SqlValue.DateTime(new DateTime(1900, 1, 2, 12, 0, 0)))
            .Should().Be(SqlValue.Integer(2));

        var path = TempDatabase("source-conversion");
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var table = await engine.CreateTableAsync("source_conversion", [
                new CatalogColumn(new ColumnId(1), "money_as_int", SqlType.Int, false, computedExpression: "amount"),
                new CatalogColumn(new ColumnId(2), "numeric_as_int", SqlType.Int, false, computedExpression: "exact"),
                new CatalogColumn(new ColumnId(3), "amount", SqlType.Money, false),
                new CatalogColumn(new ColumnId(4), "exact", SqlType.Decimal(10, 2), false)
            ]);
            var storage = await engine.OpenTableAsync(table.Id);
            var rowId = await storage.InsertAsync(new Row([
                SqlValue.Default, SqlValue.Default, SqlValue.Decimal(-10.5m), SqlValue.Decimal(-10.5m)
            ]));
            (await storage.GetAsync(rowId))!.Row.Values.Should().Equal(
                SqlValue.Integer(-11), SqlValue.Integer(-10),
                SqlValue.Decimal(-10.5000m), SqlValue.Decimal(-10.50m));
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public void SqlServerTableColumnCapacityRulesAreEnforced()
    {
        var ordinary = Enumerable.Range(1, 1_025).Select(index =>
            new CatalogColumn(new ColumnId((ulong)index), $"c{index}", SqlType.Int, true)).ToArray();
        ((Func<CatalogTable>)(() => new CatalogTable(new TableId(1), "too_wide", 1, new PageId(1), ordinary)))
            .Should().Throw<ArgumentException>();

        var wide = new List<CatalogColumn> {
            new(new ColumnId(1), "id", SqlType.Int, false),
            new(new ColumnId(2), "all_sparse", SqlType.Xml, true, isColumnSet: true)
        };
        wide.AddRange(Enumerable.Range(3, 1_023).Select(index =>
            new CatalogColumn(new ColumnId((ulong)index), $"s{index}", SqlType.Int, true, isSparse: true)));
        new CatalogTable(new TableId(2), "wide", 1, new PageId(2), wide).Columns.Should().HaveCount(1_025);
    }

    private static string TempDatabase(string purpose) =>
        Path.Combine(Path.GetTempPath(), $"sql-{purpose}-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".wal")) File.Delete(path + ".wal");
    }
}
