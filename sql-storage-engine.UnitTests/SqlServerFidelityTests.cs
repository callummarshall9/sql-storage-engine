using System.Buffers.Binary;
using System.Text;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Overflow;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class SqlServerFidelityTests
{
    private const string RootSchema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="root" type="xs:string" />
        </xs:schema>
        """;
    private const string OtherSchema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:other">
          <xs:element name="other" type="xs:int" />
        </xs:schema>
        """;

    [Test]
    public void CodePagesFixedPaddingAndTemporalAssignmentFollowDeclaredFacets()
    {
        var japanese = SqlType.VarChar(10, "Japanese_CI_AS");
        var schema = new TableDefinition([
            new ColumnDefinition(new ColumnId(1), "jp", japanese, false),
            new ColumnDefinition(new ColumnId(2), "c", SqlType.Char(4), false),
            new ColumnDefinition(new ColumnId(3), "b", SqlType.Binary(4), false),
            new ColumnDefinition(new ColumnId(4), "t", SqlType.Time(3), false),
            new ColumnDefinition(new ColumnId(5), "dt", SqlType.DateTime, false)
        ]);
        var row = new Row([SqlValue.Text("日本"), SqlValue.Text("ab"), SqlValue.Binary([1, 2]),
            SqlValue.Time(new TimeOnly(12, 0).Add(TimeSpan.FromTicks(9_999))),
            SqlValue.DateTime(new DateTime(2026, 1, 1).AddMilliseconds(2))]);

        var actual = RowCodec.Decode(RowCodec.Encode(row, schema), schema);

        actual.Values[0].Should().Be(SqlValue.Text("日本"));
        actual.Values[1].Should().Be(SqlValue.Text("ab  "));
        actual.Values[2].Should().Be(SqlValue.Binary([1, 2, 0, 0]));
        ((TimeSqlValue)actual.Values[3]).Value.Ticks.Should().Be(TimeSpan.FromHours(12).Ticks + 10_000);
        ((DateTimeSqlValue)actual.Values[4]).Value.TimeOfDay.Ticks.Should().Be(33_333);
        ((Action)(() => new ColumnDefinition(new ColumnId(1), "v", SqlType.VarChar(20), false)
            .Validate(SqlValue.Text("🌍")))).Should().Throw<ArgumentException>();
        new ColumnDefinition(new ColumnId(1), "v", SqlType.VarChar(20, "Latin1_General_100_CI_AS_UTF8"), false)
            .Validate(SqlValue.Text("🌍"));

        Key(SqlType.Decimal(10, 2), SqlValue.Decimal(1.005m)).Should().Be(
            Key(SqlType.Decimal(10, 2), SqlValue.Decimal(1.01m)));
        Key(SqlType.Binary(4), SqlValue.Binary([1, 2])).Should().Be(
            Key(SqlType.Binary(4), SqlValue.Binary([1, 2, 0, 0])));
        Key(SqlType.DateTime, SqlValue.DateTime(new DateTime(2026, 1, 1).AddMilliseconds(2))).Should().Be(
            Key(SqlType.DateTime, SqlValue.DateTime(new DateTime(2026, 1, 1).AddTicks(33_333))));
    }

    [Test]
    public void NativeJsonAndXmlUseVersionedBinaryPayloadsAndRoundTrip()
    {
        var schema = new TableDefinition([
            new ColumnDefinition(new ColumnId(1), "j", SqlType.Json, false),
            new ColumnDefinition(new ColumnId(2), "x", SqlType.Xml, false)
        ]);
        var row = new Row([SqlValue.Text("{\"a\":[1,true] }"), SqlValue.Text("<root>value</root>")]);

        var encoded = RowCodec.Encode(row, schema);
        var decoded = RowCodec.Decode(encoded, schema);

        ((TextSqlValue)decoded.Values[0]).Value.Should().Be("{\"a\":[1,true]}");
        decoded.Values[1].Should().Be(row.Values[1]);
        var variableTableOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(12)));
        var jsonOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(variableTableOffset + 4)));
        var xmlOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(variableTableOffset + 16)));
        encoded[jsonOffset].Should().Be(1, "native JSON starts with its binary format version");
        encoded[xmlOffset].Should().Be(1, "native XML starts with its binary format version");
    }

    [Test]
    public async Task RowVersionIsDatabaseGeneratedMonotonicUpdatedAndDurable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rowversion-{Guid.NewGuid():N}.db");
        try
        {
            RowId firstId;
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var table = await engine.CreateTableAsync("versions", [
                    new CatalogColumn(new ColumnId(1), "value", SqlType.Int, false),
                    new CatalogColumn(new ColumnId(2), "rv", SqlType.RowVersion, false)
                ]);
                var storage = await engine.OpenTableAsync(table.Id);
                firstId = await storage.InsertAsync(new Row([SqlValue.Integer(1), SqlValue.Null]));
                var first = await storage.GetAsync(firstId);
                ReadRowVersion(first!.Row).Should().Be(1);
                await storage.UpdateAsync(firstId, new RowUpdate([new ColumnUpdate(0, SqlValue.Integer(2))]));
                ReadRowVersion((await storage.GetAsync(firstId))!.Row).Should().Be(2);
            }
            await using (var reopened = await StorageEngine.OpenAsync(path))
            {
                var table = reopened.Catalog.Tables.Single(); var storage = await reopened.OpenTableAsync(table.Id);
                var next = await storage.InsertAsync(new Row([SqlValue.Integer(3), SqlValue.Binary(new byte[8])]));
                ReadRowVersion((await storage.GetAsync(next))!.Row).Should().Be(3);
            }
            ((Func<CatalogTable>)(() => new CatalogTable(new TableId(1), "bad", 1, new PageId(1), [
                new CatalogColumn(new ColumnId(1), "a", SqlType.RowVersion, false),
                new CatalogColumn(new ColumnId(2), "b", SqlType.Timestamp, false)
            ]))).Should().Throw<ArgumentException>();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Test]
    public async Task XmlCollectionsAssembliesAndCompleteTableTypeMetadataPersist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"catalog-fidelity-{Guid.NewGuid():N}.db");
        var collection = new SqlXmlSchemaCollection("dbo", "Schemas", RootSchema);
        try
        {
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                await engine.CreateXmlSchemaCollectionAsync(collection);
                await engine.CreateAssemblyAsync(new CatalogAssembly("Types", [1, 2, 3], "1.2.3.4", "neutral",
                    "0011223344556677"));
                var native = SqlType.ClrUserDefined("dbo", "NativePoint", "Types", "Types.NativePoint",
                    SqlClrSerializationFormat.Native, isByteOrdered: true, isFixedLength: true,
                    validationMethodName: "Validate");
                await engine.CreateScalarTypeAsync(native);
                await engine.CreateTableTypeAsync("dbo", "Batch", [
                    new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false, identity: new CatalogIdentity(10, 5)),
                    new CatalogColumn(new ColumnId(2), "guid", SqlType.UniqueIdentifier, false,
                        defaultExpression: "newid()", isRowGuidCol: true),
                    new CatalogColumn(new ColumnId(3), "document", SqlType.TypedXml(collection), true),
                    new CatalogColumn(new ColumnId(4), "calculated", SqlType.Int, true,
                        computedExpression: "id + 1", isComputedPersisted: true)
                ], [new CatalogTableTypeIndex("IX_Batch", false, true,
                    [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)],
                    CatalogIndexStorageKind.NonClustered, [new ColumnId(2)], ignoreDuplicateKey: true)],
                    [new CatalogCheckConstraint("CK_Batch", "id > 0")], isMemoryOptimized: false);
                await engine.AlterXmlSchemaCollectionAsync("dbo", "Schemas", [OtherSchema]);
            }

            await using var reopened = await StorageEngine.OpenAsync(path);
            reopened.Catalog.XmlSchemaCollections.Single().Definitions.Should().HaveCount(2);
            reopened.Catalog.Assemblies.Single().Image.ToArray().Should().Equal(1, 2, 3);
            reopened.Catalog.ScalarTypes.Single().Definition.ClrMaxByteSize.Should().BeNull();
            var tableType = reopened.Catalog.TableTypes.Single();
            tableType.IsMemoryOptimized.Should().BeFalse();
            tableType.Columns[0].Identity.Should().Be(new CatalogIdentity(10, 5));
            tableType.Columns[1].IsRowGuidCol.Should().BeTrue();
            tableType.Columns[3].IsComputedPersisted.Should().BeTrue();
            tableType.CheckConstraints.Should().ContainSingle();
            tableType.Indexes.Single().IncludedColumns.Should().Equal(new ColumnId(2));
            await ((Func<Task>)(async () => await reopened.DropXmlSchemaCollectionAsync("dbo", "Schemas")))
                .Should().ThrowAsync<CatalogConflictException>();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Test]
    public void ClrRuntimeHierarchySpatialVectorCursorAndTableValuesAreCallable()
    {
        var clr = SqlType.ClrUserDefined("dbo", "PositiveByte", "Types", "Types.PositiveByte",
            SqlClrSerializationFormat.Native, validationMethodName: "Validate");
        SqlClrRuntime.Register("Types", "Types.PositiveByte", new PositiveByteRuntime());
        try
        {
            SqlClrRuntime.Parse(clr, "7").Should().Be(SqlValue.Binary([7]));
            new ColumnDefinition(new ColumnId(1), "v", clr, false).Validate(SqlValue.Binary([7]));
            ((Action)(() => new ColumnDefinition(new ColumnId(1), "v", clr, false)
                .Validate(SqlValue.Binary([0])))).Should().Throw<ArgumentException>();
            ((Func<SqlType>)(() => SqlType.ClrUserDefined("dbo", "Bad", "Types", "Bad",
                SqlClrSerializationFormat.Native, 8))).Should().Throw<ArgumentException>();
        }
        finally { SqlClrRuntime.Unregister("Types", "Types.PositiveByte"); }

        var parent = SqlHierarchyId.Parse("/1/"); var child = SqlHierarchyId.Parse("/1/2/");
        child.IsDescendantOf(parent).Should().BeTrue();
        child.GetAncestor(1).Should().Be(parent);
        parent.GetDescendant(null, null).Should().Be(SqlHierarchyId.Parse("/1/1/"));
        var london = new SqlSpatial(SqlSpatialKind.Geography, 4326, "POINT (-0.1276 51.5072)");
        var paris = new SqlSpatial(SqlSpatialKind.Geography, 4326, "POINT (2.3522 48.8566)");
        london.STDistance(paris).Should().BeInRange(340_000, 350_000);
        var vector = (VectorSqlValue)SqlValue.Vector([1f, 0f]);
        vector.DistanceTo((VectorSqlValue)SqlValue.Vector([0f, 1f])).Should().BeApproximately(1, 0.000001);

        var definition = new TableDefinition([new ColumnDefinition(new ColumnId(1), "id", SqlType.Int, false)]);
        var table = (TableSqlValue)SqlValue.Table(definition, [new Row([SqlValue.Integer(1)])]);
        table.Rows.Should().ContainSingle();
        var cursor = (CursorSqlValue)SqlValue.Cursor(table.Rows);
        cursor = cursor.FetchNext(out var fetched); fetched.Should().Be(table.Rows[0]);
        cursor.FetchNext(out fetched); fetched.Should().BeNull();
    }

    [Test]
    public async Task JsonSpatialAndVectorIndexesAreDurableAndMaintainExactKeys()
    {
        var path = Path.Combine(Path.GetTempPath(), $"special-index-{Guid.NewGuid():N}.db");
        try
        {
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var table = await engine.CreateTableAsync("searchable", [
                    new CatalogColumn(new ColumnId(1), "json", SqlType.Json, false),
                    new CatalogColumn(new ColumnId(2), "point", SqlType.Geometry, false),
                    new CatalogColumn(new ColumnId(3), "embedding", SqlType.Vector(3), false),
                    new CatalogColumn(new ColumnId(4), "overflow", SqlType.NVarCharMax(), false),
                    new CatalogColumn(new ColumnId(5), "id", SqlType.Int, false)
                ]);
                var row = new Row([SqlValue.Text("{\"customer\":{\"id\":42}}"), SqlValue.Geometry("POINT (1 2)"),
                    SqlValue.Vector([1f, 2f, 3f]), SqlValue.Text(new string('x', 2_000)), SqlValue.Integer(1)]);
                var storage = await engine.OpenTableAsync(table.Id); var rowId = await storage.InsertAsync(row);
                await storage.InsertAsync(new Row([SqlValue.Text("{\"customer\":{\"id\":99}}"),
                    SqlValue.Geometry("POINT (20 20)"), SqlValue.Vector([9f, 9f, 9f]),
                    SqlValue.Text(new string('y', 2_000)), SqlValue.Integer(2)]));
                for (var id = 3; id <= 100; id++)
                    await storage.InsertAsync(new Row([SqlValue.Text($"{{\"customer\":{{\"id\":{id + 1_000}}}}}"),
                        SqlValue.Geometry($"POINT ({id} {id})"), SqlValue.Vector([(float)id, id, id]),
                        SqlValue.Text("seed"), SqlValue.Integer(id)]));
                await engine.CreateIndexAsync("searchable_pk", table.Id, true,
                    [new CatalogIndexedColumn(new ColumnId(5), SortDirection.Ascending, NullSortOrder.First)],
                    new CatalogBTreeIndexOptions(CatalogIndexStorageKind.Clustered, isPrimaryKey: true));
                var json = await engine.CreateSpecializedIndexAsync("json_idx", table.Id,
                    new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First),
                    CatalogSpecializedIndexOptions.Json("$.customer.id"));
                var spatial = await engine.CreateSpecializedIndexAsync("spatial_idx", table.Id,
                    new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.First),
                    CatalogSpecializedIndexOptions.Spatial());
                var vector = await engine.CreateSpecializedIndexAsync("vector_idx", table.Id,
                    new CatalogIndexedColumn(new ColumnId(3), SortDirection.Ascending, NullSortOrder.First),
                    CatalogSpecializedIndexOptions.Vector());
                (await (await engine.OpenIndexAsync(json.Id)).FindAsync([row.Values[0]])).Should().Equal(rowId);
                (await (await engine.OpenIndexAsync(spatial.Id)).FindAsync([row.Values[1]])).Should().Equal(rowId);
                (await (await engine.OpenIndexAsync(vector.Id)).FindAsync([row.Values[2]])).Should().Equal(rowId);
                var nearest = await (await engine.OpenIndexAsync(vector.Id)).SearchNearestAsync(
                    SqlValue.Vector([1f, 2f, 3f]), 1);
                nearest.Should().ContainSingle().Which.RowId.Should().Be(rowId);
            }
            await using var reopened = await StorageEngine.OpenAsync(path);
            reopened.Catalog.Indexes.Select(index => index.Method).Should().BeEquivalentTo(
                [CatalogIndexMethod.BTree, CatalogIndexMethod.Json, CatalogIndexMethod.Spatial, CatalogIndexMethod.Vector]);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Test]
    public void LobLimitsCoverSqlServerMaxPayloads()
    {
        OverflowReferenceCodec.MaximumValueLength.Should().Be(int.MaxValue);
        StorageLimits.MaximumValueBytes.Should().Be(int.MaxValue);
        OverflowReferenceCodec.MaximumChainLength.Should().BeGreaterThan(500_000);
    }

    private static ulong ReadRowVersion(Row row) => BinaryPrimitives.ReadUInt64BigEndian(
        ((BinarySqlValue)row.Values[1]).Value.Span);

    private static Indexes.IndexKey Key(SqlType type, SqlValue value)
    {
        var table = new CatalogTable(new TableId(1), "t", 1, new PageId(1),
            [new CatalogColumn(new ColumnId(1), "v", type, false)]);
        var index = new CatalogIndex(new IndexId(1), "i", table.Id, new PageId(2), false,
            [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)]);
        return CatalogIndexKey.EncodeValues([value], table, index);
    }

    private sealed class PositiveByteRuntime : ISqlClrTypeRuntime
    {
        public ReadOnlyMemory<byte> Parse(string text) => new[] { byte.Parse(text) };
        public string Format(ReadOnlyMemory<byte> serializedValue) => serializedValue.Span[0].ToString();
        public bool Validate(ReadOnlyMemory<byte> serializedValue) => serializedValue.Length == 1 && serializedValue.Span[0] > 0;
        public int Compare(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) => left.Span[0].CompareTo(right.Span[0]);
    }
}
