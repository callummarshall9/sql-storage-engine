// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
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
public sealed class ExtendedSqlTypeTests
{
    private static readonly Guid Identifier = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    [Test]
    public void EveryExtendedType_RoundTripsThroughFixedRowFormat()
    {
        var expected = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(-1234567890.123456789m), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(-123.5), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateSqlValue(new DateOnly(2026, 8, 11)), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(new TimeOnly(23, 59, 58, 999, 999)), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2026, 8, 11, 21, 30, 45, DateTimeKind.Utc)), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeOffsetSqlValue(new DateTimeOffset(2026, 8, 11, 21, 30, 45, TimeSpan.FromHours(5.5))), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateUniqueIdentifierSqlValue(Identifier)]);
        var actual = sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(expected,Schema()),Schema());
        actual.Values.Should().Equal(expected.Values);
        ((DateTimeSqlValue)actual.Values[4]).Value.Kind.Should().Be(DateTimeKind.Unspecified);
        ((DateTimeOffsetSqlValue)actual.Values[5]).Value.Offset.Should().Be(TimeSpan.FromHours(5.5));
    }

    [Test]
    public void ExtendedClrRepresentations_AreConvertedAndValidated()
    {
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFromSqlValue(12.25m).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(12.25m));
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFromSqlValue(12.25d).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(12.25d));
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFromSqlValue(12.25f).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(12.25d));
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFromSqlValue(new DateOnly(2026, 8, 11)).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateSqlValue(new DateOnly(2026, 8, 11)));
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFromSqlValue(new TimeOnly(1, 2, 3)).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(new TimeOnly(1, 2, 3)));
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFromSqlValue(Identifier).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateUniqueIdentifierSqlValue(Identifier));
        ((Func<SqlValue>)(() => sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(double.NaN))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<SqlValue>)(() => sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(double.PositiveInfinity))).Should().Throw<ArgumentOutOfRangeException>();
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(-0d).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(0d));
    }

    [Test]
    public void InvalidPersistedDecimalFloatAndTemporalValues_AreRejected()
    {
        var schema = Schema();
        var encoded = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(1m), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(1d), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateSqlValue(DateOnly.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(TimeOnly.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(DateTime.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeOffsetSqlValue(DateTimeOffset.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateUniqueIdentifierSqlValue(Guid.Empty)]),schema);
        // The fixed region starts after the 32-byte header and one-byte null bitmap.
        encoded[33] = 2; // Decimal sign is encoded as zero or one.
        ((Func<Row>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(encoded,schema))).Should().Throw<StorageFormatException>();
        encoded = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(1m), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(1d), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateSqlValue(DateOnly.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(TimeOnly.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(DateTime.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeOffsetSqlValue(DateTimeOffset.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateUniqueIdentifierSqlValue(Guid.Empty)]),schema);
        BinaryPrimitives.WriteDoubleLittleEndian(encoded.AsSpan(33 + 17), double.NaN);
        ((Func<Row>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(encoded,schema))).Should().Throw<StorageFormatException>();
        encoded = sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(1m), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(1d), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateSqlValue(DateOnly.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(TimeOnly.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(DateTime.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeOffsetSqlValue(DateTimeOffset.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateUniqueIdentifierSqlValue(Guid.Empty)]),schema);
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(33 + 17 + 8), int.MaxValue);
        ((Func<Row>)(() => sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(encoded,schema))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void ExtendedIndexKeys_FollowLogicalOrderAndCanonicalEquality()
    {
        AssertIndexOrder(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(31,2), [sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(decimal.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(-1m), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(0m), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(.01m), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(decimal.MaxValue)]);
        AssertIndexOrder(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateFloatSqlType(), [sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(-double.MaxValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(-1d), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(0d), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(.01d), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(double.MaxValue)]);
        AssertIndexOrder(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDate(), [sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateSqlValue(DateOnly.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateSqlValue(new DateOnly(2026, 8, 11)), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateSqlValue(DateOnly.MaxValue)]);
        AssertIndexOrder(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateTimeSqlType(), [sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(TimeOnly.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(new TimeOnly(12, 0)), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(TimeOnly.MaxValue)]);
        AssertIndexOrder(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTime2SqlType(), [sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(DateTime.MinValue), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2026, 8, 11)), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(DateTime.MaxValue)]);
        AssertIndexOrder(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeUniqueIdentifier(), [sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateUniqueIdentifierSqlValue(Guid.Empty), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateUniqueIdentifierSqlValue(Identifier), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateUniqueIdentifierSqlValue(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"))]);
        EncodeKey(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(38,2),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(1m)).Should().Be(EncodeKey(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(38,2),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(1.00m)));
        EncodeKey(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTimeOffsetSqlType(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeOffsetSqlValue(new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero))).Should().Be(EncodeKey(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTimeOffsetSqlType(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeOffsetSqlValue(new DateTimeOffset(2026, 8, 11, 13, 0, 0, TimeSpan.FromHours(1)))));
    }

    [Test]
    public void Catalog_RoundTripsEveryExtendedTypeTag()
    {
        var types = RepresentativeTypes();
        var columns = types.Select((type, index) => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = checked((ulong)index + 1) }, $"{type}_{index}", type, true)).ToArray();
        var catalog = sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "all_types", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 2 }, columns)], []);
        sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(catalog)).Tables.Single().Columns.Select(column => column.Type).Should().Equal(types);
    }

    [Test]
    public async Task PublicStorageApi_PersistsExtendedValuesAndIndexesAcrossReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"extended-types-{Guid.NewGuid():N}.db");
        var expected = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(123.4500m), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(-12.5d), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateSqlValue(new DateOnly(2026, 8, 11)), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(new TimeOnly(12, 34, 56)), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2026, 8, 11, 12, 34, 56)), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeOffsetSqlValue(new DateTimeOffset(2026, 8, 11, 12, 34, 56, TimeSpan.FromHours(-4))), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateUniqueIdentifierSqlValue(Identifier)]);
        try
        {
            await using (var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var columns = Schema().Columns.Select(column => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(column.Id, column.Name, column.Type, column.IsNullable));
                var table = await engine.CreateCatalogTableAsync("extended", columns);
                await engine.CreateCatalogIndexAsync("by_decimal", table.Id, false, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.Last)]);
                await (await engine.OpenTableAsync(table.Id)).InsertAsync(expected);
            }

            await using var reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var tableDefinition = reopened.ReadCatalog().ReadTables().Single();
            var rows = new List<StoredRow>();
            await foreach (var row in (await reopened.OpenTableAsync(tableDefinition.Id)).ScanAsync())
                rows.Add(row);
            rows.Should().ContainSingle();
            SqlValueExposure.CompareSqlValues([rows[0].Row.Values[0], expected.Values[0]]).Should().Be(SqlComparison.Equal);
            rows[0].Row.Values.Skip(1).Should().Equal(expected.Values.Skip(1));
            var index = reopened.ReadCatalog().ReadIndexes().Single();
            (await (await reopened.OpenIndexAsync(index.Id)).FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(123.45m)])).Should().Equal(rows[0].RowId);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void AssertIndexOrder(SqlType type, IReadOnlyList<SqlValue> values)
    {
        for (var index = 1; index < values.Count; index++)
        {
            sql_storage_engine.Exposures.Rows.SqlValueExposure.CompareSqlValues([values[index - 1], values[index]]).Should().Be(SqlComparison.Less);
            sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CompareIndexKeys(indexKeys: [EncodeKey(type, values[index - 1]), EncodeKey(type, values[index])]).Should().BeLessThan(0);
        }
    }

    private static sql_storage_engine.Models.Indexes.IndexKey EncodeKey(SqlType type, SqlValue value)
    {
        var columnId = new sql_storage_engine.Models.Identifiers.ColumnId
        {
            Value = 1
        };
        var table = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "t", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(columnId, "c", type, false)]);
        var index = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 },name: "i",tableId: table.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 2 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(columnId,SortDirection.Ascending,NullSortOrder.Last)]);
        return sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.EncodeValues([value],table,index);
    }

    private static TableDefinition Schema() => sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition([sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"decimal",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(38,9),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"float",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateFloatSqlType(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },"date",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDate(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 },"time",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateTimeSqlType(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 5 },"datetime",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTime2SqlType(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 6 },"datetimeoffset",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTimeOffsetSqlType(),false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 7 },"identifier",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeUniqueIdentifier(),false)]);
    private static SqlType[] RepresentativeTypes() => [sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBit(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTinyInt(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSmallInt(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(38,10), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNumericSqlType(12,3), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSmallMoney(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeMoney(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeReal(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateFloatSqlType(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDate(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateTimeSqlType(3), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSmallDateTime(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDateTime(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTime2SqlType(7), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTimeOffsetSqlType(7), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateCharSqlType(8,"Latin1_General_100_BIN2"), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(20), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeText(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNCharSqlType(8), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(20), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeNText(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateBinarySqlType(8), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarBinarySqlType(20), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeImage(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTimestamp(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeUniqueIdentifier(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSqlVariant(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeHierarchyId(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeGeometry(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeGeography(), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(3)];
}