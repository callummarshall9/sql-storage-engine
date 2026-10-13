using System.Numerics;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

namespace sql_storage_engine.UnitTests;
public sealed class SqlServerTypeTests
{
    [Test]
    public void EverySqlServer2025SystemTypeName_HasACanonicalDeclaration()
    {
        var declarations = new[]
        {
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBit(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTinyInt(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSmallInt(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNumericSqlType(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSmallMoney(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeMoney(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeReal(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateFloatSqlType(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDate(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateTimeSqlType(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSmallDateTime(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDateTime(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTime2SqlType(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTimeOffsetSqlType(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateCharSqlType(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeText(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNCharSqlType(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeNText(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateBinarySqlType(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarBinarySqlType(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeImage(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTimestamp(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeUniqueIdentifier(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSqlVariant(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeHierarchyId(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeGeometry(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeGeography(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(1),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeCursor(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTable()
        };
        var expectedNames = Enum.GetValues<SqlTypeName>().Except([SqlTypeName.RowVersion]);
        declarations.Select(type => type.Name).Distinct().Should().BeEquivalentTo(expectedNames);
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeRowVersion().Should().Be(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTimestamp(), "rowversion is normalized synonym metadata");
    }

    [Test]
    public void ExactNumericAndLengthFacets_EnforceSqlServerRanges()
    {
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTinyInt(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(255)).Should().NotThrow();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTinyInt(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(-1)).Should().Throw<ArgumentOutOfRangeException>();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSmallInt(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(short.MinValue)).Should().NotThrow();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue((long)int.MaxValue + 1)).Should().Throw<ArgumentOutOfRangeException>();
        var maximum = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(sql_storage_engine.Exposures.Rows.SqlDecimalExposure.ParseSqlDecimal("99999999999999999999999999999999999999"));
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(38,0),maximum).Should().NotThrow();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(37,0),maximum).Should().Throw<ArgumentOutOfRangeException>();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(10,2),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(1.001m)).Should().NotThrow("assignment rounds to scale");
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeMoney(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(sql_storage_engine.Exposures.Rows.SqlDecimalExposure.ParseSqlDecimal("922337203685477.5807"))).Should().NotThrow();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeMoney(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(sql_storage_engine.Exposures.Rows.SqlDecimalExposure.ParseSqlDecimal("922337203685477.5808"))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<SqlValue>)(() => sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(double.NaN))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<SqlValue>)(() => sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(double.PositiveInfinity))).Should().Throw<ArgumentOutOfRangeException>();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(2),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("éa")).Should().NotThrow(); // Two bytes in Windows-1252.
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(2,"Latin1_General_100_CI_AS_UTF8"),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("éa")).Should().Throw<ArgumentOutOfRangeException>();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(2),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("🌍")).Should().NotThrow(); // One surrogate pair.
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateCharSqlType(3),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("ab")).Should().NotThrow("SQL pads fixed character values");
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateBinarySqlType(3),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([1, 2])).Should().NotThrow("SQL pads fixed binary values");
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarBinarySqlType(2),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([1, 2, 3])).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void TemporalJsonXmlVectorAndVariantFacets_AreValidated()
    {
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateTimeSqlType(3),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(new TimeOnly(12, 0, 0, 123))).Should().NotThrow();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateTimeSqlType(3),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(new TimeOnly(12, 0).Add(TimeSpan.FromTicks(1)))).Should().NotThrow("assignment rounds to the declared temporal scale");
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSmallDateTime(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2079, 6, 6, 23, 59, 0))).Should().NotThrow();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSmallDateTime(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2079, 6, 7))).Should().Throw<ArgumentOutOfRangeException>();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDateTime(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2026, 1, 1, 0, 0, 0, 3))).Should().NotThrow();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDateTime(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2026, 1, 1).AddTicks(1))).Should().NotThrow();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<root><value /></root>")).Should().NotThrow();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<root>")).Should().Throw<ArgumentException>();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("{\"value\":1}")).Should().NotThrow();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("{value:1}")).Should().Throw<ArgumentException>();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(3),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([1f, 2f, 3f])).Should().NotThrow();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(2),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([1f, 2f, 3f])).Should().Throw<ArgumentException>();
        ((Func<SqlValue>)(() => sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVariantSqlValue(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<x />")))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void EveryStorableSqlServerFamily_RoundTripsThroughOneRow()
    {
        var id = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var columns = new (SqlType Type, SqlValue Value)[]
        {
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBit(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBooleanSqlValue(true)),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTinyInt(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(255)),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSmallInt(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(short.MinValue)),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(int.MaxValue)),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(long.MinValue)),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(38,0), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(sql_storage_engine.Exposures.Rows.SqlDecimalExposure.ParseSqlDecimal("99999999999999999999999999999999999999"))),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNumericSqlType(20,4), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(123.4500m)),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSmallMoney(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(-214748.3648m)),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeMoney(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(sql_storage_engine.Exposures.Rows.SqlDecimalExposure.ParseSqlDecimal("922337203685477.5807"))),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeReal(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue((double)12.5f)),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateFloatSqlType(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateFloatSqlValue(double.MaxValue)),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDate(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateSqlValue(new DateOnly(2026, 8, 11))),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateTimeSqlType(3), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTimeSqlValue(new TimeOnly(12, 34, 56, 123))),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSmallDateTime(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2026, 8, 11, 12, 34, 0))),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDateTime(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2026, 8, 11, 12, 34, 56).AddTicks(33_333))),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTime2SqlType(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeSqlValue(new DateTime(2026, 8, 11, 12, 34, 56).AddTicks(7))),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDateTimeOffsetSqlType(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDateTimeOffsetSqlValue(new DateTimeOffset(2026, 8, 11, 12, 34, 56, TimeSpan.FromHours(1)))),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateCharSqlType(3), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("abc")),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(10), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("héllo")),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharMaxSqlType(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("max text")),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeText(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("legacy text")),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNCharSqlType(2), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("🌍")),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(20), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("Unicode 🌍")),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharMaxSqlType(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("max Unicode 🌍")),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeNText(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("legacy Unicode")),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateBinarySqlType(3), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([0, 1, 2])),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarBinarySqlType(8), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([3, 4, 5])),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeVarBinaryMax(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([9, 8, 7])),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeImage(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([6, 7])),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeRowVersion(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([0, 1, 2, 3, 4, 5, 6, 7])),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTimestamp(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([7, 6, 5, 4, 3, 2, 1, 0])),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeUniqueIdentifier(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateUniqueIdentifierSqlValue(id)),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<root />")),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("[1,2,3]")),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSqlVariant(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVariantSqlValue(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(42))),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeHierarchyId(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateHierarchyIdSqlValue("/1/2/")),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeGeometry(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateGeometrySqlValue("POINT (1 2)",0)),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeGeography(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateGeographySqlValue("POINT (-0.1 51.5)",4326)),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(3), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVectorSqlValue([1.5f, -2f, 3.25f])),
            (sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(3,SqlVectorBaseType.Float16), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateHalfVectorSqlValue([(Half)1.5f, (Half)(-2f), (Half)3.25f]))
        };
        var schema = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition(columns.Select((item, index) => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = checked((ulong)index + 1) }, $"c{index}", item.Type, false)));
        var expected = RowExposure.CreateRow(columns.Select(item => item.Value));
        var actual = sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(expected,schema),schema);
        actual.Values.Should().Equal(expected.Values);
    }

    [Test]
    public void Decimal38AndCollatedText_IndexKeysFollowSqlOrdering()
    {
        var decimalType = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(38,0);
        var decimals = new[]
        {
            sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(sql_storage_engine.Exposures.Rows.SqlDecimalExposure.ParseSqlDecimal("-99999999999999999999999999999999999999")),
            sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(-1m),
            sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(0m),
            sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(1m),
            sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateDecimalSqlValue(sql_storage_engine.Exposures.Rows.SqlDecimalExposure.ParseSqlDecimal("99999999999999999999999999999999999999"))
        };
        for (var index = 1; index < decimals.Length; index++)
            sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CompareIndexKeys(indexKeys: [Key(decimalType,decimals[index - 1]), Key(decimalType,decimals[index])]).Should().BeLessThan(0);
        var textType = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(20,"Latin1_General_100_CI_AI");
        Key(textType,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("A")).Should().Be(Key(textType,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("a")));
        sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CompareIndexKeys(indexKeys: [Key(textType,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("aa")), Key(textType,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("b"))]).Should().BeLessThan(0);
        Key(textType,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("a")).Should().Be(Key(textType,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("a ")));
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
        var actual = values.OrderBy(value => Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeUniqueIdentifier(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateUniqueIdentifierSqlValue(value)).Content, comparer: System.StringComparer.Ordinal).ToArray();
        actual.Should().Equal(expected);
    }

    [Test]
    public void SqlVariant_RoundTripsNestedCharacterFacetsIncludingCollation()
    {
        var nestedType = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(20,"Latin1_General_100_CI_AI");
        var schema = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition([sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"value",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSqlVariant(),false)]);
        var expected = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVariantSqlValue(nestedType,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("résumé"))]);
        var actual = sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(expected,schema),schema);
        actual.Values.Should().Equal(expected.Values);
        ((VariantSqlValue)actual.Values.Single()).DeclaredType.Should().Be(nestedType);
        Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSqlVariant(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVariantSqlValue(nestedType,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("A"))).Should().Be(Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSqlVariant(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVariantSqlValue(nestedType,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("a"))));
        var intTwo = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVariantSqlValue(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2));
        var bigintTwo = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVariantSqlValue(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2));
        var bigintHundred = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateVariantSqlValue(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(100));
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CompareSqlValues(sqlValues: [intTwo, bigintTwo]).Should().Be(SqlComparison.Equal);
        sql_storage_engine.Exposures.Rows.SqlValueExposure.CompareSqlValues(sqlValues: [bigintHundred, intTwo]).Should().Be(SqlComparison.Greater);
        Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSqlVariant(),intTwo).Should().Be(Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSqlVariant(),bigintTwo));
        sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CompareIndexKeys(indexKeys: [Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSqlVariant(),bigintHundred), Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSqlVariant(),intTwo)]).Should().BeGreaterThan(0);
    }

    [Test]
    public void NonComparableSqlServerTypes_AreRejectedAsBTreeKeys()
    {
        foreach (var type in new[]
        {
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeJson(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeImage(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeGeometry(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeGeography(),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVectorSqlType(3),
            sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeVarBinaryMax()
        }

        )
        {
            var columnId = new sql_storage_engine.Models.Identifiers.ColumnId
            {
                Value = 1
            };
            var table = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "t", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(columnId, "c", type, true)]);
            var index = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 },name: "i",tableId: table.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 2 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(columnId,SortDirection.Ascending,NullSortOrder.Last)]);
            ((Func<CatalogDefinition>)(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([table], [index]))).Should().Throw<ArgumentException>();
        }
    }

    [Test]
    public void ExecutionScopedCursorAndTableTypes_AreModeledButCannotBePersistedAsColumns()
    {
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeCanBeColumn(sqlType: sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeCursor()).Should().BeFalse();
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeCanBeColumn(sqlType: sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTable()).Should().BeFalse();
        ((Func<CatalogColumn>)(() => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "c", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeCursor(), true))).Should().Throw<ArgumentException>();
        ((Func<CatalogColumn>)(() => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "t", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTable(), true))).Should().Throw<ArgumentException>();
    }

    [TestCase(SortDirection.Ascending, NullSortOrder.First, 0, 1, 2)]
    [TestCase(SortDirection.Ascending, NullSortOrder.Last, 1, 2, 0)]
    [TestCase(SortDirection.Descending, NullSortOrder.First, 0, 2, 1)]
    [TestCase(SortDirection.Descending, NullSortOrder.Last, 2, 1, 0)]
    public void IndexDirectionDoesNotReverseRequestedNullPlacement(SortDirection direction, NullSortOrder nullOrder, params int[] expected)
    {
        SqlValue[] values = [sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)];
        var actual = Enumerable.Range(0, values.Length).OrderBy(index => Key(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),values[index],direction,nullOrder).Content, comparer: System.StringComparer.Ordinal).ToArray();
        actual.Should().Equal(expected);
    }

    private static Action Validate(SqlType type, SqlValue value) => () => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CheckColumnDefinitionValue(columnDefinition: sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"value",type,false),sqlValue: value);
    private static sql_storage_engine.Models.Indexes.IndexKey Key(SqlType type, SqlValue value) => Key(type,value,SortDirection.Ascending,NullSortOrder.Last);
    private static sql_storage_engine.Models.Indexes.IndexKey Key(SqlType type, SqlValue value, SortDirection direction, NullSortOrder nullOrder)
    {
        var columnId = new sql_storage_engine.Models.Identifiers.ColumnId
        {
            Value = 1
        };
        var table = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "t", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(columnId, "c", type, false)]);
        var index = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 },name: "i",tableId: table.Id,pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 2 },isUnique: false,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(columnId,direction,nullOrder)]);
        return sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.EncodeValues([value],table,index);
    }
}