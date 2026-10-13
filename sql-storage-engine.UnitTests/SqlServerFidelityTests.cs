using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Models.Diagnostics;
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers.Binary;
using System.Text;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

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
        var japanese = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(10,"Japanese_CI_AS");
        var schema = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition([sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"jp",japanese,false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"c",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateCharSqlType(4),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },"b",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateBinarySqlType(4),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 },"t",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateTimeSqlType(3),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 5 },"dt",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDateTime(),false)]);
        var row = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("日本"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("ab"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([1, 2]), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(new TimeOnly(12, 0).Add(TimeSpan.FromTicks(9_999))), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2026, 1, 1).AddMilliseconds(2))]);
        var actual = sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(row,schema),schema);
        actual.Values[0].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("日本"));
        actual.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("ab  "));
        actual.Values[2].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([1, 2, 0, 0]));
        ((TimeSqlValue)actual.Values[3]).Value.Ticks.Should().Be(TimeSpan.FromHours(12).Ticks + 10_000);
        ((DateTimeSqlValue)actual.Values[4]).Value.TimeOfDay.Ticks.Should().Be(33_333);
        ((Action)(() => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CheckColumnDefinitionValue(columnDefinition: sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"v",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(20),false),sqlValue: sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("🌍")))).Should().Throw<ArgumentException>();
        sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CheckColumnDefinitionValue(columnDefinition: sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"v",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(20,"Latin1_General_100_CI_AS_UTF8"),false),sqlValue: sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("🌍"));
        Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(10,2),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(1.005m)).Should().Be(Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(10,2),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(1.01m)));
        Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateBinarySqlType(4),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([1, 2])).Should().Be(Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateBinarySqlType(4),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([1, 2, 0, 0])));
        Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDateTime(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2026, 1, 1).AddMilliseconds(2))).Should().Be(Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDateTime(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2026, 1, 1).AddTicks(33_333))));
    }

    [Test]
    public void NativeJsonAndXmlUseVersionedBinaryPayloadsAndRoundTrip()
    {
        var schema = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition([sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"j",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"x",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(),false)]);
        var row = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("{\"a\":[1,true] }"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<root>value</root>")]);
        var encoded = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(row,schema);
        var decoded = sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(encoded,schema);
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
            await using (var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var table = await engine.CreateCatalogTableAsync("versions", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "rv", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeRowVersion(), false)]);
                var storage = await engine.OpenTableAsync(table.Id);
                firstId = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()]));
                var first = await storage.GetAsync(firstId);
                ReadRowVersion(first!.Row).Should().Be(1);
                await storage.UpdateAsync(firstId, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2) }]));
                ReadRowVersion((await storage.GetAsync(firstId))!.Row).Should().Be(2);
            }

            await using (var reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path))
            {
                var table = reopened.ReadCatalog().ReadTables().Single();
                var storage = await reopened.OpenTableAsync(table.Id);
                var next = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[8])]));
                ReadRowVersion((await storage.GetAsync(next))!.Row).Should().Be(3);
            }

            ((Func<CatalogTable>)(() => sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "bad", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "a", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeRowVersion(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "b", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTimestamp(), false)]))).Should().Throw<ArgumentException>();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public async Task XmlCollectionsAssembliesAndCompleteTableTypeMetadataPersist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"catalog-fidelity-{Guid.NewGuid():N}.db");
        var collection = sql_storage_engine.Exposures.Rows.SqlXmlSchemaCollectionExposure.CreateSqlXmlSchemaCollection("dbo","Schemas",RootSchema);
        try
        {
            await using (var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                await engine.CreateSqlXmlSchemaCollectionAsync(collection);
                await engine.CreateCatalogAssemblyAsync(sql_storage_engine.Exposures.Catalog.CatalogAssemblyExposure.CreateCatalogAssembly("Types",[1, 2, 3],"1.2.3.4","neutral","0011223344556677"));
                var native = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateClrUserDefinedSqlType("dbo","NativePoint","Types","Types.NativePoint",SqlClrSerializationFormat.Native,isByteOrdered: true,isFixedLength: true,validationMethodName: "Validate");
                await engine.CreateCatalogScalarTypeAsync(native);
                await engine.CreateCatalogTableTypeAsync("dbo", "Batch", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, catalogIdentity: sql_storage_engine.Exposures.Catalog.CatalogIdentityExposure.CreateCatalogIdentity(10,5)), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "guid", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeUniqueIdentifier(), false, defaultExpression: "newid()", isRowGuidCol: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "document", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateTypedXmlSqlType(collection), true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 }, "calculated", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), true, computedExpression: "id + 1", isComputedPersisted: true)], [sql_storage_engine.Exposures.Catalog.CatalogTableTypeIndexExposure.CreateCatalogTableTypeIndex("IX_Batch", false, true, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)], CatalogIndexStorageKind.NonClustered, [new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }], ignoreDuplicateKey: true)], [sql_storage_engine.Exposures.Catalog.CatalogCheckConstraintExposure.CreateCatalogCheckConstraint("CK_Batch","id > 0")], isMemoryOptimized: false);
                await engine.AlterXmlSchemaCollectionAsync("dbo", "Schemas", [OtherSchema]);
            }

            await using var reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            sql_storage_engine.Exposures.Rows.SqlXmlSchemaCollectionExposure.ReadSqlXmlSchemaCollectionDefinitions(reopened.ReadCatalog().ReadXmlSchemaCollections().Single()).Should().HaveCount(2);
            sql_storage_engine.Exposures.Catalog.CatalogAssemblyExposure.ReadCatalogAssemblyImage(reopened.ReadCatalog().ReadAssemblies().Single()).ToArray().Should().Equal(1, 2, 3);
            reopened.ReadCatalog().ReadScalarTypes().Single().Definition.ClrMaxByteSize.Should().BeNull();
            var tableType = reopened.ReadCatalog().ReadTableTypes().Single();
            tableType.IsMemoryOptimized.Should().BeFalse();
            tableType.Columns[0].Identity.Should().Be(sql_storage_engine.Exposures.Catalog.CatalogIdentityExposure.CreateCatalogIdentity(10,5));
            tableType.Columns[1].IsRowGuidCol.Should().BeTrue();
            tableType.Columns[3].IsComputedPersisted.Should().BeTrue();
            tableType.CheckConstraints.Should().ContainSingle();
            tableType.Indexes.Single().IncludedColumns.Should().Equal(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 });
            await ((Func<Task>)(async () => await reopened.DropXmlSchemaCollectionAsync("dbo", "Schemas"))).Should().ThrowAsync<CatalogConflictException>();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public void ClrRuntimeHierarchySpatialVectorCursorAndTableValuesAreCallable()
    {
        var clr = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateClrUserDefinedSqlType("dbo","PositiveByte","Types","Types.PositiveByte",SqlClrSerializationFormat.Native,validationMethodName: "Validate");
        sql_storage_engine.Exposures.Rows.SqlClrRuntimeExposure.Register("Types","Types.PositiveByte",new PositiveByteRuntime());
        try
        {
            sql_storage_engine.Exposures.Rows.SqlClrRuntimeExposure.Parse(clr,"7").Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([7]));
            sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CheckColumnDefinitionValue(columnDefinition: sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"v",clr,false),sqlValue: sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([7]));
            ((Action)(() => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CheckColumnDefinitionValue(columnDefinition: sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"v",clr,false),sqlValue: sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([0])))).Should().Throw<ArgumentException>();
            ((Func<SqlType>)(() => sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateClrUserDefinedSqlType("dbo","Bad","Types","Bad",SqlClrSerializationFormat.Native,8))).Should().Throw<ArgumentException>();
        }
        finally
        {
            sql_storage_engine.Exposures.Rows.SqlClrRuntimeExposure.Unregister("Types","Types.PositiveByte");
        }

        var parent = sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ParseSqlHierarchyId("/1/");
        var child = sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ParseSqlHierarchyId("/1/2/");
        sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.CheckSqlHierarchyIdDescendant([child, parent]).Should().BeTrue();
        sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ReadSqlHierarchyIdAncestor(sqlHierarchyId: child,1).Should().Be(parent);
        sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.CreateSqlHierarchyIdDescendant([parent, null, null]).Should().Be(sql_storage_engine.Exposures.Rows.SqlHierarchyIdExposure.ParseSqlHierarchyId("/1/1/"));
        var london = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geography,srid: 4326,wellKnownText: "POINT (-0.1276 51.5072)");
        var paris = sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.CreateSqlSpatial(kind: SqlSpatialKind.Geography,srid: 4326,wellKnownText: "POINT (2.3522 48.8566)");
        sql_storage_engine.Exposures.Spatial.SqlSpatialExposure.MeasureSqlSpatialDistance(sqlSpatials: [london, paris]).Should().BeInRange(340_000, 350_000);
        var vector = (VectorSqlValue)sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([1f, 0f]);
        sql_storage_engine.Exposures.Rows.SqlValueExposure.MeasureVectorSqlValueDistance(vectorSqlValues: [vector, (VectorSqlValue)sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([0f, 1f])]).Should().BeApproximately(1, 0.000001);
        var definition = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition([sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"id",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),false)]);
        var table = (TableSqlValue)sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTableSqlValue(definition,[RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)])]);
        table.Rows.Should().ContainSingle();
        var cursor = (CursorSqlValue)sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateCursorSqlValue(table.Rows);
        cursor = sql_storage_engine.Exposures.Rows.CursorSqlValueExposure.FetchCursorSqlValue(cursor,"Next",0,out var fetched);
        fetched.Should().Be(table.Rows[0]);
        sql_storage_engine.Exposures.Rows.CursorSqlValueExposure.FetchCursorSqlValue(cursor,"Next",0,out fetched);
        fetched.Should().BeNull();
    }

    [Test]
    public async Task JsonSpatialAndVectorIndexesAreDurableAndMaintainExactKeys()
    {
        var path = Path.Combine(Path.GetTempPath(), $"special-index-{Guid.NewGuid():N}.db");
        try
        {
            await using (var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var table = await engine.CreateCatalogTableAsync("searchable", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "json", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "point", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeGeometry(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "embedding", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(3), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 }, "overflow", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharMaxSqlType(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 5 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)]);
                var row = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("{\"customer\":{\"id\":42}}"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateGeometrySqlValue("POINT (1 2)"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([1f, 2f, 3f]), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('x', 2_000)), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]);
                var storage = await engine.OpenTableAsync(table.Id);
                var rowId = await storage.InsertAsync(row);
                await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("{\"customer\":{\"id\":99}}"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateGeometrySqlValue("POINT (20 20)"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([9f, 9f, 9f]), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('y', 2_000)), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)]));
                for (var id = 3; id <= 100; id++)
                    await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue($"{{\"customer\":{{\"id\":{id + 1_000}}}}}"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateGeometrySqlValue($"POINT ({id} {id})"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([(float)id, id, id]), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("seed"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(id)]));
                await engine.CreateCatalogIndexAsync("searchable_pk", table.Id, true, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 5 },SortDirection.Ascending,NullSortOrder.First)], sql_storage_engine.Exposures.Catalog.CatalogBTreeIndexOptionsExposure.CreateCatalogBTreeIndexOptions(CatalogIndexStorageKind.Clustered,isPrimaryKey: true));
                var json = await engine.CreateSpecializedCatalogIndexAsync("json_idx", table.Id, sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First), sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions("$.customer.id"));
                var spatial = await engine.CreateSpecializedCatalogIndexAsync("spatial_idx", table.Id, sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.First), sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateSpatialCatalogSpecializedIndexOptions());
                var vector = await engine.CreateSpecializedCatalogIndexAsync("vector_idx", table.Id, sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },SortDirection.Ascending,NullSortOrder.First), sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateVectorCatalogSpecializedIndexOptions());
                (await (await engine.OpenIndexAsync(json.Id)).FindAsync([row.Values[0]])).Should().Equal(rowId);
                (await (await engine.OpenIndexAsync(spatial.Id)).FindAsync([row.Values[1]])).Should().Equal(rowId);
                (await (await engine.OpenIndexAsync(vector.Id)).FindAsync([row.Values[2]])).Should().Equal(rowId);
                var nearest = await (await engine.OpenIndexAsync(vector.Id)).SearchNearestAsync(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([1f, 2f, 3f]), 1);
                nearest.Should().ContainSingle().Which.RowId.Should().Be(rowId);
            }

            await using var reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            reopened.ReadCatalog().ReadIndexes().Select(index => index.Method).Should().BeEquivalentTo([CatalogIndexMethod.BTree, CatalogIndexMethod.Json, CatalogIndexMethod.Spatial, CatalogIndexMethod.Vector]);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public void LobLimitsCoverSqlServerMaxPayloads()
    {
        OverflowReferenceFormat.MaximumValueLength.Should().Be(int.MaxValue);
        StorageLimits.MaximumValueBytes.Should().Be(int.MaxValue);
        OverflowReferenceFormat.MaximumChainLength.Should().BeGreaterThan(500_000);
    }

    private static ulong ReadRowVersion(Row row) => BinaryPrimitives.ReadUInt64BigEndian(sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadBinarySqlValue(binarySqlValue: ((BinarySqlValue)row.Values[1])).Span);
    private static sql_storage_engine.Models.Indexes.IndexKey Key(SqlType type, SqlValue value)
    {
        var table = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "t", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "v", type, false)]);
        var index = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 },name: "i",tableId: table.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 2 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)]);
        return sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.EncodeValues([value],table,index);
    }

    private sealed class PositiveByteRuntime : ISqlClrTypeRuntime
    {
        public ReadOnlyMemory<byte> Parse(string text) => new[]
        {
            byte.Parse(text)
        };
        public string Format(ReadOnlyMemory<byte> serializedValue) => serializedValue.Span[0].ToString();
        public bool Validate(ReadOnlyMemory<byte> serializedValue) => serializedValue.Length == 1 && serializedValue.Span[0] > 0;
        public int Compare(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) => left.Span[0].CompareTo(right.Span[0]);
    }
}