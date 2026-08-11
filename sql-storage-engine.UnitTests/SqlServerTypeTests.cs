using System.Numerics;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class SqlServerTypeTests
{
    [Test]
    public void EverySqlServer2025SystemTypeName_HasACanonicalDeclaration()
    {
        var declarations = new[]
        {
            SqlType.Bit, SqlType.TinyInt, SqlType.SmallInt, SqlType.Int, SqlType.BigInt,
            SqlType.Decimal(), SqlType.Numeric(), SqlType.SmallMoney, SqlType.Money, SqlType.Real, SqlType.Float(),
            SqlType.Date, SqlType.Time(), SqlType.SmallDateTime, SqlType.DateTime, SqlType.DateTime2(),
            SqlType.DateTimeOffset(), SqlType.Char(), SqlType.VarChar(), SqlType.Text, SqlType.NChar(),
            SqlType.NVarChar(), SqlType.NText, SqlType.Binary(), SqlType.VarBinary(), SqlType.Image,
            SqlType.Timestamp, SqlType.UniqueIdentifier, SqlType.Xml, SqlType.Json, SqlType.SqlVariant,
            SqlType.HierarchyId, SqlType.Geometry, SqlType.Geography, SqlType.Vector(1), SqlType.Cursor, SqlType.Table
        };
        var expectedNames = Enum.GetValues<SqlTypeName>().Except([SqlTypeName.RowVersion]);

        declarations.Select(type => type.Name).Distinct().Should().BeEquivalentTo(expectedNames);
        SqlType.RowVersion.Should().Be(SqlType.Timestamp, "rowversion is normalized synonym metadata");
    }

    [Test]
    public void ExactNumericAndLengthFacets_EnforceSqlServerRanges()
    {
        Validate(SqlType.TinyInt, SqlValue.Integer(255)).Should().NotThrow();
        Validate(SqlType.TinyInt, SqlValue.Integer(-1)).Should().Throw<ArgumentOutOfRangeException>();
        Validate(SqlType.SmallInt, SqlValue.Integer(short.MinValue)).Should().NotThrow();
        Validate(SqlType.Int, SqlValue.Integer((long)int.MaxValue + 1)).Should().Throw<ArgumentOutOfRangeException>();

        var maximum = SqlValue.Decimal(SqlDecimal.Parse("99999999999999999999999999999999999999"));
        Validate(SqlType.Decimal(38, 0), maximum).Should().NotThrow();
        Validate(SqlType.Decimal(37, 0), maximum).Should().Throw<ArgumentOutOfRangeException>();
        Validate(SqlType.Decimal(10, 2), SqlValue.Decimal(1.001m)).Should().NotThrow("assignment rounds to scale");
        Validate(SqlType.Money, SqlValue.Decimal(SqlDecimal.Parse("922337203685477.5807"))).Should().NotThrow();
        Validate(SqlType.Money, SqlValue.Decimal(SqlDecimal.Parse("922337203685477.5808")))
            .Should().Throw<ArgumentOutOfRangeException>();
        ((Func<SqlValue>)(() => SqlValue.Float(double.NaN))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<SqlValue>)(() => SqlValue.Float(double.PositiveInfinity))).Should().Throw<ArgumentOutOfRangeException>();

        Validate(SqlType.VarChar(2), SqlValue.Text("éa")).Should().NotThrow(); // Two bytes in Windows-1252.
        Validate(SqlType.VarChar(2, "Latin1_General_100_CI_AS_UTF8"), SqlValue.Text("éa"))
            .Should().Throw<ArgumentOutOfRangeException>();
        Validate(SqlType.NVarChar(2), SqlValue.Text("🌍")).Should().NotThrow(); // One surrogate pair.
        Validate(SqlType.Char(3), SqlValue.Text("ab")).Should().NotThrow("SQL pads fixed character values");
        Validate(SqlType.Binary(3), SqlValue.Binary([1, 2])).Should().NotThrow("SQL pads fixed binary values");
        Validate(SqlType.VarBinary(2), SqlValue.Binary([1, 2, 3])).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void TemporalJsonXmlVectorAndVariantFacets_AreValidated()
    {
        Validate(SqlType.Time(3), SqlValue.Time(new TimeOnly(12, 0, 0, 123))).Should().NotThrow();
        Validate(SqlType.Time(3), SqlValue.Time(new TimeOnly(12, 0).Add(TimeSpan.FromTicks(1))))
            .Should().NotThrow("assignment rounds to the declared temporal scale");
        Validate(SqlType.SmallDateTime, SqlValue.DateTime(new DateTime(2079, 6, 6, 23, 59, 0))).Should().NotThrow();
        Validate(SqlType.SmallDateTime, SqlValue.DateTime(new DateTime(2079, 6, 7))).Should().Throw<ArgumentOutOfRangeException>();
        Validate(SqlType.DateTime, SqlValue.DateTime(new DateTime(2026, 1, 1, 0, 0, 0, 3))).Should().NotThrow();
        Validate(SqlType.DateTime, SqlValue.DateTime(new DateTime(2026, 1, 1).AddTicks(1))).Should().NotThrow();

        Validate(SqlType.Xml, SqlValue.Text("<root><value /></root>")).Should().NotThrow();
        Validate(SqlType.Xml, SqlValue.Text("<root>")).Should().Throw<ArgumentException>();
        Validate(SqlType.Json, SqlValue.Text("{\"value\":1}")).Should().NotThrow();
        Validate(SqlType.Json, SqlValue.Text("{value:1}")).Should().Throw<ArgumentException>();
        Validate(SqlType.Vector(3), SqlValue.Vector([1f, 2f, 3f])).Should().NotThrow();
        Validate(SqlType.Vector(2), SqlValue.Vector([1f, 2f, 3f])).Should().Throw<ArgumentException>();
        ((Func<SqlValue>)(() => SqlValue.Variant(SqlType.Xml, SqlValue.Text("<x />"))))
            .Should().Throw<ArgumentException>();
    }

    [Test]
    public void EveryStorableSqlServerFamily_RoundTripsThroughOneRow()
    {
        var id = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var columns = new (SqlType Type, SqlValue Value)[]
        {
            (SqlType.Bit, SqlValue.Boolean(true)),
            (SqlType.TinyInt, SqlValue.Integer(255)),
            (SqlType.SmallInt, SqlValue.Integer(short.MinValue)),
            (SqlType.Int, SqlValue.Integer(int.MaxValue)),
            (SqlType.BigInt, SqlValue.Integer(long.MinValue)),
            (SqlType.Decimal(38, 0), SqlValue.Decimal(SqlDecimal.Parse("99999999999999999999999999999999999999"))),
            (SqlType.Numeric(20, 4), SqlValue.Decimal(123.4500m)),
            (SqlType.SmallMoney, SqlValue.Decimal(-214748.3648m)),
            (SqlType.Money, SqlValue.Decimal(SqlDecimal.Parse("922337203685477.5807"))),
            (SqlType.Real, SqlValue.Float((double)12.5f)),
            (SqlType.Float(), SqlValue.Float(double.MaxValue)),
            (SqlType.Date, SqlValue.Date(new DateOnly(2026, 8, 11))),
            (SqlType.Time(3), SqlValue.Time(new TimeOnly(12, 34, 56, 123))),
            (SqlType.SmallDateTime, SqlValue.DateTime(new DateTime(2026, 8, 11, 12, 34, 0))),
            (SqlType.DateTime, SqlValue.DateTime(new DateTime(2026, 8, 11, 12, 34, 56).AddTicks(33_333))),
            (SqlType.DateTime2(7), SqlValue.DateTime(new DateTime(2026, 8, 11, 12, 34, 56).AddTicks(7))),
            (SqlType.DateTimeOffset(7), SqlValue.DateTimeOffset(new DateTimeOffset(2026, 8, 11, 12, 34, 56, TimeSpan.FromHours(1)))),
            (SqlType.Char(3), SqlValue.Text("abc")),
            (SqlType.VarChar(10), SqlValue.Text("héllo")),
            (SqlType.VarCharMax(), SqlValue.Text("max text")),
            (SqlType.Text, SqlValue.Text("legacy text")),
            (SqlType.NChar(2), SqlValue.Text("🌍")),
            (SqlType.NVarChar(20), SqlValue.Text("Unicode 🌍")),
            (SqlType.NVarCharMax(), SqlValue.Text("max Unicode 🌍")),
            (SqlType.NText, SqlValue.Text("legacy Unicode")),
            (SqlType.Binary(3), SqlValue.Binary([0, 1, 2])),
            (SqlType.VarBinary(8), SqlValue.Binary([3, 4, 5])),
            (SqlType.VarBinaryMax, SqlValue.Binary([9, 8, 7])),
            (SqlType.Image, SqlValue.Binary([6, 7])),
            (SqlType.RowVersion, SqlValue.Binary([0, 1, 2, 3, 4, 5, 6, 7])),
            (SqlType.Timestamp, SqlValue.Binary([7, 6, 5, 4, 3, 2, 1, 0])),
            (SqlType.UniqueIdentifier, SqlValue.UniqueIdentifier(id)),
            (SqlType.Xml, SqlValue.Text("<root />")),
            (SqlType.Json, SqlValue.Text("[1,2,3]")),
            (SqlType.SqlVariant, SqlValue.Variant(SqlType.Int, SqlValue.Integer(42))),
            (SqlType.HierarchyId, SqlValue.HierarchyId("/1/2/")),
            (SqlType.Geometry, SqlValue.Geometry("POINT (1 2)", 0)),
            (SqlType.Geography, SqlValue.Geography("POINT (-0.1 51.5)", 4326)),
            (SqlType.Vector(3), SqlValue.Vector([1.5f, -2f, 3.25f])),
            (SqlType.Vector(3, SqlVectorBaseType.Float16),
                SqlValue.HalfVector([(Half)1.5f, (Half)(-2f), (Half)3.25f]))
        };
        var schema = new TableDefinition(columns.Select((item, index) =>
            new ColumnDefinition(new ColumnId(checked((ulong)index + 1)), $"c{index}", item.Type, false)));
        var expected = new Row(columns.Select(item => item.Value));

        var actual = RowCodec.Decode(RowCodec.Encode(expected, schema), schema);

        actual.Values.Should().Equal(expected.Values);
    }

    [Test]
    public void Decimal38AndCollatedText_IndexKeysFollowSqlOrdering()
    {
        var decimalType = SqlType.Decimal(38, 0);
        var decimals = new[]
        {
            SqlValue.Decimal(SqlDecimal.Parse("-99999999999999999999999999999999999999")),
            SqlValue.Decimal(-1m), SqlValue.Decimal(0m), SqlValue.Decimal(1m),
            SqlValue.Decimal(SqlDecimal.Parse("99999999999999999999999999999999999999"))
        };
        for (var index = 1; index < decimals.Length; index++)
            Key(decimalType, decimals[index - 1]).CompareTo(Key(decimalType, decimals[index])).Should().BeLessThan(0);

        var textType = SqlType.VarChar(20, "Latin1_General_100_CI_AI");
        Key(textType, SqlValue.Text("A")).Should().Be(Key(textType, SqlValue.Text("a")));
        Key(textType, SqlValue.Text("aa")).CompareTo(Key(textType, SqlValue.Text("b"))).Should().BeLessThan(0);
        Key(textType, SqlValue.Text("a")).Should().Be(Key(textType, SqlValue.Text("a ")));
    }

    [Test]
    public void UniqueIdentifierKeys_FollowSqlGuidRatherThanClrGuidOrdering()
    {
        var values = new[]
        {
            Guid.Parse("01000000-0000-0000-0000-000000000000"),
            Guid.Parse("00000000-0100-0000-0000-000000000000"),
            Guid.Parse("00000000-0000-0100-0000-000000000000"),
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")
        };
        var expected = values.OrderBy(value => new System.Data.SqlTypes.SqlGuid(value)).ToArray();
        var actual = values.OrderBy(value => Key(SqlType.UniqueIdentifier, SqlValue.UniqueIdentifier(value))).ToArray();

        actual.Should().Equal(expected);
    }

    [Test]
    public void SqlVariant_RoundTripsNestedCharacterFacetsIncludingCollation()
    {
        var nestedType = SqlType.VarChar(20, "Latin1_General_100_CI_AI");
        var schema = new TableDefinition([
            new ColumnDefinition(new ColumnId(1), "value", SqlType.SqlVariant, false)
        ]);
        var expected = new Row([SqlValue.Variant(nestedType, SqlValue.Text("résumé"))]);

        var actual = RowCodec.Decode(RowCodec.Encode(expected, schema), schema);

        actual.Values.Should().Equal(expected.Values);
        ((VariantSqlValue)actual.Values.Single()).DeclaredType.Should().Be(nestedType);
        Key(SqlType.SqlVariant, SqlValue.Variant(nestedType, SqlValue.Text("A"))).Should().Be(
            Key(SqlType.SqlVariant, SqlValue.Variant(nestedType, SqlValue.Text("a"))));

        var intTwo = SqlValue.Variant(SqlType.Int, SqlValue.Integer(2));
        var bigintTwo = SqlValue.Variant(SqlType.BigInt, SqlValue.Integer(2));
        var bigintHundred = SqlValue.Variant(SqlType.BigInt, SqlValue.Integer(100));
        SqlValue.Compare(intTwo, bigintTwo).Should().Be(SqlComparison.Equal);
        SqlValue.Compare(bigintHundred, intTwo).Should().Be(SqlComparison.Greater);
        Key(SqlType.SqlVariant, intTwo).Should().Be(Key(SqlType.SqlVariant, bigintTwo));
        Key(SqlType.SqlVariant, bigintHundred).CompareTo(Key(SqlType.SqlVariant, intTwo)).Should().BeGreaterThan(0);
    }

    [Test]
    public void NonComparableSqlServerTypes_AreRejectedAsBTreeKeys()
    {
        foreach (var type in new[] { SqlType.Xml, SqlType.Json, SqlType.Image, SqlType.Geometry,
                     SqlType.Geography, SqlType.Vector(3), SqlType.VarBinaryMax })
        {
            var columnId = new ColumnId(1);
            var table = new CatalogTable(new TableId(1), "t", 1, new PageId(1),
                [new CatalogColumn(columnId, "c", type, true)]);
            var index = new CatalogIndex(new IndexId(1), "i", table.Id, new PageId(2), false,
                [new CatalogIndexedColumn(columnId, SortDirection.Ascending, NullSortOrder.Last)]);
            ((Func<CatalogDefinition>)(() => new CatalogDefinition([table], [index])))
                .Should().Throw<ArgumentException>();
        }
    }

    [Test]
    public void ExecutionScopedCursorAndTableTypes_AreModeledButCannotBePersistedAsColumns()
    {
        SqlType.Cursor.CanBeColumn.Should().BeFalse();
        SqlType.Table.CanBeColumn.Should().BeFalse();
        ((Func<CatalogColumn>)(() => new CatalogColumn(new ColumnId(1), "c", SqlType.Cursor, true)))
            .Should().Throw<ArgumentException>();
        ((Func<CatalogColumn>)(() => new CatalogColumn(new ColumnId(1), "t", SqlType.Table, true)))
            .Should().Throw<ArgumentException>();
    }

    [TestCase(SortDirection.Ascending, NullSortOrder.First, 0, 1, 2)]
    [TestCase(SortDirection.Ascending, NullSortOrder.Last, 1, 2, 0)]
    [TestCase(SortDirection.Descending, NullSortOrder.First, 0, 2, 1)]
    [TestCase(SortDirection.Descending, NullSortOrder.Last, 2, 1, 0)]
    public void IndexDirectionDoesNotReverseRequestedNullPlacement(SortDirection direction,
        NullSortOrder nullOrder, params int[] expected)
    {
        SqlValue[] values = [SqlValue.Null, SqlValue.Integer(1), SqlValue.Integer(2)];
        var actual = Enumerable.Range(0, values.Length).OrderBy(index =>
            Key(SqlType.Int, values[index], direction, nullOrder)).ToArray();
        actual.Should().Equal(expected);
    }

    private static Action Validate(SqlType type, SqlValue value) =>
        () => new ColumnDefinition(new ColumnId(1), "value", type, false).Validate(value);

    private static Indexes.IndexKey Key(SqlType type, SqlValue value)
        => Key(type, value, SortDirection.Ascending, NullSortOrder.Last);

    private static Indexes.IndexKey Key(SqlType type, SqlValue value, SortDirection direction,
        NullSortOrder nullOrder)
    {
        var columnId = new ColumnId(1);
        var table = new CatalogTable(new TableId(1), "t", 1, new PageId(1),
            [new CatalogColumn(columnId, "c", type, false)]);
        var index = new CatalogIndex(new IndexId(1), "i", table.Id, new PageId(2), false,
            [new CatalogIndexedColumn(columnId, direction, nullOrder)]);
        return CatalogIndexKey.EncodeValues([value], table, index);
    }
}
