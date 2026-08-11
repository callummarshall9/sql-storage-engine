using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class ExtendedSqlTypeTests
{
    private static readonly Guid Identifier = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

    [Test]
    public void EveryExtendedType_RoundTripsThroughFixedRowFormat()
    {
        var expected = new Row([
            SqlValue.Decimal(-1234567890.123456789m),
            SqlValue.Float(-123.5),
            SqlValue.Date(new DateOnly(2026, 8, 11)),
            SqlValue.Time(new TimeOnly(23, 59, 58, 999, 999)),
            SqlValue.DateTime(new DateTime(2026, 8, 11, 21, 30, 45, DateTimeKind.Utc)),
            SqlValue.DateTimeOffset(new DateTimeOffset(2026, 8, 11, 21, 30, 45, TimeSpan.FromHours(5.5))),
            SqlValue.UniqueIdentifier(Identifier)
        ]);

        var actual = RowCodec.Decode(RowCodec.Encode(expected, Schema()), Schema());

        actual.Values.Should().Equal(expected.Values);
        ((DateTimeSqlValue)actual.Values[4]).Value.Kind.Should().Be(DateTimeKind.Unspecified);
        ((DateTimeOffsetSqlValue)actual.Values[5]).Value.Offset.Should().Be(TimeSpan.FromHours(5.5));
    }

    [Test]
    public void ExtendedClrRepresentations_AreConvertedAndValidated()
    {
        SqlValue.From(12.25m).Should().Be(SqlValue.Decimal(12.25m));
        SqlValue.From(12.25d).Should().Be(SqlValue.Float(12.25d));
        SqlValue.From(12.25f).Should().Be(SqlValue.Float(12.25d));
        SqlValue.From(new DateOnly(2026, 8, 11)).Should().Be(SqlValue.Date(new DateOnly(2026, 8, 11)));
        SqlValue.From(new TimeOnly(1, 2, 3)).Should().Be(SqlValue.Time(new TimeOnly(1, 2, 3)));
        SqlValue.From(Identifier).Should().Be(SqlValue.UniqueIdentifier(Identifier));
        ((Func<SqlValue>)(() => SqlValue.Float(double.NaN))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<SqlValue>)(() => SqlValue.Float(double.PositiveInfinity))).Should().Throw<ArgumentOutOfRangeException>();
        SqlValue.Float(-0d).Should().Be(SqlValue.Float(0d));
    }

    [Test]
    public void InvalidPersistedDecimalFloatAndTemporalValues_AreRejected()
    {
        var schema = Schema();
        var encoded = RowCodec.Encode(new Row([
            SqlValue.Decimal(1m), SqlValue.Float(1d), SqlValue.Date(DateOnly.MinValue), SqlValue.Time(TimeOnly.MinValue),
            SqlValue.DateTime(DateTime.MinValue), SqlValue.DateTimeOffset(DateTimeOffset.MinValue),
            SqlValue.UniqueIdentifier(Guid.Empty)
        ]), schema);

        // The fixed region starts after the 32-byte header and one-byte null bitmap.
        encoded[33] = 2; // Decimal sign is encoded as zero or one.
        ((Func<Row>)(() => RowCodec.Decode(encoded, schema))).Should().Throw<StorageFormatException>();

        encoded = RowCodec.Encode(new Row([
            SqlValue.Decimal(1m), SqlValue.Float(1d), SqlValue.Date(DateOnly.MinValue), SqlValue.Time(TimeOnly.MinValue),
            SqlValue.DateTime(DateTime.MinValue), SqlValue.DateTimeOffset(DateTimeOffset.MinValue),
            SqlValue.UniqueIdentifier(Guid.Empty)
        ]), schema);
        BinaryPrimitives.WriteDoubleLittleEndian(encoded.AsSpan(33 + 17), double.NaN);
        ((Func<Row>)(() => RowCodec.Decode(encoded, schema))).Should().Throw<StorageFormatException>();

        encoded = RowCodec.Encode(new Row([
            SqlValue.Decimal(1m), SqlValue.Float(1d), SqlValue.Date(DateOnly.MinValue), SqlValue.Time(TimeOnly.MinValue),
            SqlValue.DateTime(DateTime.MinValue), SqlValue.DateTimeOffset(DateTimeOffset.MinValue),
            SqlValue.UniqueIdentifier(Guid.Empty)
        ]), schema);
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(33 + 17 + 8), int.MaxValue);
        ((Func<Row>)(() => RowCodec.Decode(encoded, schema))).Should().Throw<StorageFormatException>();
    }

    [Test]
    public void ExtendedIndexKeys_FollowLogicalOrderAndCanonicalEquality()
    {
        AssertIndexOrder(SqlType.Decimal(31, 2),
            [SqlValue.Decimal(decimal.MinValue), SqlValue.Decimal(-1m), SqlValue.Decimal(0m), SqlValue.Decimal(.01m), SqlValue.Decimal(decimal.MaxValue)]);
        AssertIndexOrder(SqlType.Float(),
            [SqlValue.Float(-double.MaxValue), SqlValue.Float(-1d), SqlValue.Float(0d), SqlValue.Float(.01d), SqlValue.Float(double.MaxValue)]);
        AssertIndexOrder(SqlType.Date,
            [SqlValue.Date(DateOnly.MinValue), SqlValue.Date(new DateOnly(2026, 8, 11)), SqlValue.Date(DateOnly.MaxValue)]);
        AssertIndexOrder(SqlType.Time(),
            [SqlValue.Time(TimeOnly.MinValue), SqlValue.Time(new TimeOnly(12, 0)), SqlValue.Time(TimeOnly.MaxValue)]);
        AssertIndexOrder(SqlType.DateTime2(),
            [SqlValue.DateTime(DateTime.MinValue), SqlValue.DateTime(new DateTime(2026, 8, 11)), SqlValue.DateTime(DateTime.MaxValue)]);
        AssertIndexOrder(SqlType.UniqueIdentifier,
            [SqlValue.UniqueIdentifier(Guid.Empty), SqlValue.UniqueIdentifier(Identifier), SqlValue.UniqueIdentifier(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"))]);

        EncodeKey(SqlType.Decimal(38, 2), SqlValue.Decimal(1m)).Should().Be(EncodeKey(SqlType.Decimal(38, 2), SqlValue.Decimal(1.00m)));
        EncodeKey(SqlType.DateTimeOffset(), SqlValue.DateTimeOffset(new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero)))
            .Should().Be(EncodeKey(SqlType.DateTimeOffset(),
                SqlValue.DateTimeOffset(new DateTimeOffset(2026, 8, 11, 13, 0, 0, TimeSpan.FromHours(1)))));
    }

    [Test]
    public void Catalog_RoundTripsEveryExtendedTypeTag()
    {
        var types = RepresentativeTypes();
        var columns = types.Select((type, index) =>
            new CatalogColumn(new ColumnId(checked((ulong)index + 1)), $"{type}_{index}", type, true)).ToArray();
        var catalog = new CatalogDefinition([
            new CatalogTable(new TableId(1), "all_types", 1, new PageId(2), columns)
        ], []);

        CatalogCodec.Decode(CatalogCodec.Encode(catalog)).Tables.Single().Columns
            .Select(column => column.Type).Should().Equal(types);
    }

    [Test]
    public async Task PublicStorageApi_PersistsExtendedValuesAndIndexesAcrossReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"extended-types-{Guid.NewGuid():N}.db");
        var expected = new Row([
            SqlValue.Decimal(123.4500m), SqlValue.Float(-12.5d), SqlValue.Date(new DateOnly(2026, 8, 11)),
            SqlValue.Time(new TimeOnly(12, 34, 56)), SqlValue.DateTime(new DateTime(2026, 8, 11, 12, 34, 56)),
            SqlValue.DateTimeOffset(new DateTimeOffset(2026, 8, 11, 12, 34, 56, TimeSpan.FromHours(-4))),
            SqlValue.UniqueIdentifier(Identifier)
        ]);
        try
        {
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var columns = Schema().Columns.Select(column =>
                    new CatalogColumn(column.Id, column.Name, column.Type, column.IsNullable));
                var table = await engine.CreateTableAsync("extended", columns);
                await engine.CreateIndexAsync("by_decimal", table.Id, false,
                    [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.Last)]);
                await (await engine.OpenTableAsync(table.Id)).InsertAsync(expected);
            }

            await using var reopened = await StorageEngine.OpenAsync(path);
            var tableDefinition = reopened.Catalog.Tables.Single();
            var rows = new List<StoredRow>();
            await foreach (var row in (await reopened.OpenTableAsync(tableDefinition.Id)).ScanAsync()) rows.Add(row);
            rows.Should().ContainSingle();
            rows[0].Row.Values.Should().Equal(expected.Values);
            var index = reopened.Catalog.Indexes.Single();
            (await (await reopened.OpenIndexAsync(index.Id)).FindAsync([SqlValue.Decimal(123.45m)]))
                .Should().Equal(rows[0].RowId);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void AssertIndexOrder(SqlType type, IReadOnlyList<SqlValue> values)
    {
        for (var index = 1; index < values.Count; index++)
        {
            SqlValue.Compare(values[index - 1], values[index]).Should().Be(SqlComparison.Less);
            EncodeKey(type, values[index - 1]).CompareTo(EncodeKey(type, values[index])).Should().BeLessThan(0);
        }
    }

    private static Indexes.IndexKey EncodeKey(SqlType type, SqlValue value)
    {
        var columnId = new ColumnId(1);
        var table = new CatalogTable(new TableId(1), "t", 1, new PageId(1),
            [new CatalogColumn(columnId, "c", type, false)]);
        var index = new CatalogIndex(new IndexId(1), "i", table.Id, new PageId(2), false,
            [new CatalogIndexedColumn(columnId, SortDirection.Ascending, NullSortOrder.Last)]);
        return CatalogIndexKey.EncodeValues([value], table, index);
    }

    private static TableDefinition Schema() => new([
        new ColumnDefinition(new ColumnId(1), "decimal", SqlType.Decimal(38, 9), false),
        new ColumnDefinition(new ColumnId(2), "float", SqlType.Float(), false),
        new ColumnDefinition(new ColumnId(3), "date", SqlType.Date, false),
        new ColumnDefinition(new ColumnId(4), "time", SqlType.Time(), false),
        new ColumnDefinition(new ColumnId(5), "datetime", SqlType.DateTime2(), false),
        new ColumnDefinition(new ColumnId(6), "datetimeoffset", SqlType.DateTimeOffset(), false),
        new ColumnDefinition(new ColumnId(7), "identifier", SqlType.UniqueIdentifier, false)
    ]);

    private static SqlType[] RepresentativeTypes() =>
    [
        SqlType.Bit, SqlType.TinyInt, SqlType.SmallInt, SqlType.Int, SqlType.BigInt,
        SqlType.Decimal(38, 10), SqlType.Numeric(12, 3), SqlType.SmallMoney, SqlType.Money,
        SqlType.Real, SqlType.Float(), SqlType.Date, SqlType.Time(3), SqlType.SmallDateTime,
        SqlType.DateTime, SqlType.DateTime2(7), SqlType.DateTimeOffset(7),
        SqlType.Char(8, "Latin1_General_100_BIN2"), SqlType.VarChar(20), SqlType.Text,
        SqlType.NChar(8), SqlType.NVarChar(20), SqlType.NText, SqlType.Binary(8),
        SqlType.VarBinary(20), SqlType.Image, SqlType.Timestamp, SqlType.UniqueIdentifier,
        SqlType.Xml, SqlType.Json, SqlType.SqlVariant, SqlType.HierarchyId, SqlType.Geometry,
        SqlType.Geography, SqlType.Vector(3)
    ];
}
