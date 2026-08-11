using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class UserDefinedSqlTypeTests
{
    private const string InvoiceSchema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="invoice">
            <xs:complexType>
              <xs:sequence><xs:element name="total" type="xs:decimal" /></xs:sequence>
            </xs:complexType>
          </xs:element>
        </xs:schema>
        """;
    private const string CustomerSchema = """
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:customer"
                   xmlns="urn:customer" elementFormDefault="qualified">
          <xs:element name="customer" type="xs:string" />
        </xs:schema>
        """;

    [Test]
    public void SqlSpellingSynonyms_NormalizeToCanonicalSystemTypes()
    {
        SqlType.Dec(12, 3).Should().Be(SqlType.Decimal(12, 3));
        SqlType.Numeric(12, 3).Name.Should().Be(SqlTypeName.Numeric);
        SqlType.DoublePrecision.Should().Be(SqlType.Float(53));
        SqlType.Float(1).Precision.Should().Be(24);
        SqlType.Float(25).Precision.Should().Be(53);
        SqlType.Character().Should().Be(SqlType.Char(1));
        SqlType.CharacterVarying(20).Should().Be(SqlType.VarChar(20));
        SqlType.BinaryVarying(20).Should().Be(SqlType.VarBinary(20));
        SqlType.NationalCharacter(20).Should().Be(SqlType.NChar(20));
        SqlType.NationalCharVarying(20).Should().Be(SqlType.NVarChar(20));
        SqlType.NationalText.Should().Be(SqlType.NText);
        SqlType.Integer.Should().Be(SqlType.Int);
        SqlType.Timestamp.Should().Be(SqlType.RowVersion);
        SqlType.SysName.Should().Be(SqlType.NVarChar(128));
    }

    [Test]
    public void TypedXml_EnforcesSchemaCollectionAndDocumentConstraint()
    {
        var collection = new SqlXmlSchemaCollection("dbo", "InvoiceSchema", [InvoiceSchema, CustomerSchema]);
        var type = SqlType.TypedXml(collection, SqlXmlContentKind.Document);

        Validate(type, SqlValue.Text("<invoice><total>12.34</total></invoice>")).Should().NotThrow();
        Validate(type, SqlValue.Text("<invoice><total>not-a-number</total></invoice>"))
            .Should().Throw<ArgumentException>();
        Validate(type, SqlValue.Text("<invoice><total>1</total></invoice><invoice><total>2</total></invoice>"))
            .Should().Throw<ArgumentException>();
        type.ToString().Should().Be("xml(document [dbo].[InvoiceSchema])");
        type.XmlSchemaCollection!.Definitions.Should().HaveCount(2);
    }

    [Test]
    public void AliasAndClrTypes_RoundTripThroughRowsAndEnforceTheirContracts()
    {
        var accountNumber = SqlType.Alias("sales", "AccountNumber", SqlType.VarChar(12), false);
        var point = SqlType.ClrUserDefined("dbo", "Point", "SpatialTypes", "Samples.Point",
            SqlClrSerializationFormat.UserDefined, 16, isByteOrdered: true, isFixedLength: true,
            validationMethodName: "ValidatePoint");
        var schema = new TableDefinition([
            new ColumnDefinition(new ColumnId(1), "account", accountNumber, false),
            new ColumnDefinition(new ColumnId(2), "point", point, false)
        ]);
        var expected = new Row([SqlValue.Text("A-123"), SqlValue.Binary(Enumerable.Range(0, 16).Select(i => (byte)i).ToArray())]);

        RowCodec.Decode(RowCodec.Encode(expected, schema), schema).Values.Should().Equal(expected.Values);
        accountNumber.QualifiedUserTypeName.Should().Be("[sales].[AccountNumber]");
        point.CanBeBTreeKey.Should().BeTrue();
        Validate(point, SqlValue.Binary(new byte[15])).Should().Throw<ArgumentException>();
        SqlType.ClrUserDefined("dbo", "Opaque", "Types", "Opaque", SqlClrSerializationFormat.UserDefined, -1)
            .CanBeBTreeKey.Should().BeFalse();
        SqlType.ClrUserDefined("dbo", "Opaque", "Types", SqlClrSerializationFormat.UserDefined, -1)
            .ClrClassName.Should().Be("Opaque");
        ((Func<SqlType>)(() => SqlType.Alias("dbo", "Bad", SqlType.Xml)))
            .Should().Throw<ArgumentException>();
        Validate(SqlType.HierarchyId, SqlValue.Binary(new byte[893])).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ScalarAndTableTypes_RoundTripThroughCatalogMetadata()
    {
        var accountNumber = SqlType.Alias("sales", "AccountNumber", SqlType.VarChar(12), false);
        var point = SqlType.ClrUserDefined("dbo", "Point", "SpatialTypes", "Samples.Point",
            SqlClrSerializationFormat.UserDefined, 16, isByteOrdered: true, isFixedLength: true,
            validationMethodName: "ValidatePoint");
        var typedXml = SqlType.TypedXml(new SqlXmlSchemaCollection("dbo", "BusinessSchemas",
            [InvoiceSchema, CustomerSchema]), SqlXmlContentKind.Document);
        var tableType = new CatalogTableType("sales", "AccountBatch", [
            new CatalogColumn(new ColumnId(1), "account", accountNumber, false),
            new CatalogColumn(new ColumnId(2), "point", point, false),
            new CatalogColumn(new ColumnId(3), "payload", typedXml, true)
        ], [new CatalogTableTypeIndex("PK_AccountBatch", true, true,
            [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)])]);
        var table = new CatalogTable(new TableId(1), "accounts", 1, new PageId(3),
            [new CatalogColumn(new ColumnId(1), "account", accountNumber, false)]);
        var expected = new CatalogDefinition([table], [],
            [new CatalogScalarType(accountNumber), new CatalogScalarType(point)], [tableType]);

        var actual = CatalogCodec.Decode(CatalogCodec.Encode(expected));

        actual.ScalarTypes.Should().BeEquivalentTo(expected.ScalarTypes);
        actual.TableTypes.Should().BeEquivalentTo(expected.TableTypes);
        actual.Tables.Should().BeEquivalentTo(expected.Tables);
    }

    [Test]
    public void TypedXmlCatalogMetadata_CanExceedLegacyUInt16StringLimit()
    {
        var documentation = new string('x', 70_000);
        var xsd = $"""
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:annotation><xs:documentation>{documentation}</xs:documentation></xs:annotation>
              <xs:element name="root" type="xs:string" />
            </xs:schema>
            """;
        var type = SqlType.TypedXml(new SqlXmlSchemaCollection("dbo", "LargeSchema", xsd));
        var catalog = new CatalogDefinition([
            new CatalogTable(new TableId(1), "documents", 1, new PageId(2),
                [new CatalogColumn(new ColumnId(1), "value", type, false)])
        ], []);

        CatalogCodec.Decode(CatalogCodec.Encode(catalog)).Tables.Single().Columns.Single().Type.Should().Be(type);
    }

    [Test]
    public async Task PublicCatalogApi_PersistsNamedTypesAcrossReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sql-storage-types-{Guid.NewGuid():N}.db");
        var accountNumber = SqlType.Alias("sales", "AccountNumber", SqlType.VarChar(12), false);
        try
        {
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                await engine.CreateScalarTypeAsync(accountNumber);
                await engine.CreateTableTypeAsync("sales", "AccountBatch",
                    [new CatalogColumn(new ColumnId(1), "account", accountNumber, false)]);
                await engine.CreateTableAsync("accounts",
                    [new CatalogColumn(new ColumnId(1), "account", accountNumber, false)]);
            }

            await using var reopened = await StorageEngine.OpenAsync(path);
            var resolvedAccountNumber = SqlType.Alias("sales", "AccountNumber",
                SqlType.VarChar(12, reopened.Catalog.DefaultCollation), false);
            reopened.Catalog.TryGetScalarType("sales", "AccountNumber", out var scalar).Should().BeTrue();
            scalar!.Definition.Should().Be(resolvedAccountNumber);
            reopened.Catalog.TryGetTableType("sales", "AccountBatch", out var tableType).Should().BeTrue();
            tableType!.Columns.Single().Type.Should().Be(resolvedAccountNumber);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static Action Validate(SqlType type, SqlValue value) =>
        () => new ColumnDefinition(new ColumnId(1), "value", type, false).Validate(value);
}
