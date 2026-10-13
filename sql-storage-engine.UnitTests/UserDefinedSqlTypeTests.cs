using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

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
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecSqlType(12,3).Should().Be(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateDecimalSqlType(12,3));
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNumericSqlType(12,3).Name.Should().Be(SqlTypeName.Numeric);
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeDoublePrecision().Should().Be(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateFloatSqlType(53));
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateFloatSqlType(1).Precision.Should().Be(24);
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateFloatSqlType(25).Precision.Should().Be(53);
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateCharacterSqlType().Should().Be(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateCharSqlType(1));
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateCharacterVaryingSqlType(20).Should().Be(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(20));
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateBinaryVaryingSqlType(20).Should().Be(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarBinarySqlType(20));
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNationalCharacterSqlType(20).Should().Be(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNCharSqlType(20));
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNationalCharVaryingSqlType(20).Should().Be(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(20));
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeNationalText().Should().Be(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeNText());
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInteger().Should().Be(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt());
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeTimestamp().Should().Be(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeRowVersion());
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeSysName().Should().Be(sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(128));
    }

    [Test]
    public void TypedXml_EnforcesSchemaCollectionAndDocumentConstraint()
    {
        var collection = sql_storage_engine.Exposures.Rows.SqlXmlSchemaCollectionExposure.CreateSqlXmlSchemaCollection("dbo","InvoiceSchema",[InvoiceSchema, CustomerSchema]);
        var type = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateTypedXmlSqlType(collection,SqlXmlContentKind.Document);
        Validate(type,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<invoice><total>12.34</total></invoice>")).Should().NotThrow();
        Validate(type,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<invoice><total>not-a-number</total></invoice>")).Should().Throw<ArgumentException>();
        Validate(type,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<invoice><total>1</total></invoice><invoice><total>2</total></invoice>")).Should().Throw<ArgumentException>();
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.FormatSqlType(sqlType: type).Should().Be("xml(document [dbo].[InvoiceSchema])");
        sql_storage_engine.Exposures.Rows.SqlXmlSchemaCollectionExposure.ReadSqlXmlSchemaCollectionDefinitions(sqlXmlSchemaCollection: type.XmlSchemaCollection!).Should().HaveCount(2);
    }

    [Test]
    public void AliasAndClrTypes_RoundTripThroughRowsAndEnforceTheirContracts()
    {
        var accountNumber = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateAliasSqlType("sales","AccountNumber",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(12),false);
        var point = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateClrUserDefinedSqlType("dbo","Point","SpatialTypes","Samples.Point",SqlClrSerializationFormat.UserDefined,16,isByteOrdered: true,isFixedLength: true,validationMethodName: "ValidatePoint");
        var schema = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition([sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"account",accountNumber,false), sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"point",point,false)]);
        var expected = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("A-123"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(Enumerable.Range(0, 16).Select(i => (byte)i).ToArray())]);
        sql_storage_engine.Exposures.Rows.RowCodecExposure.Decode(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(expected,schema),schema).Values.Should().Equal(expected.Values);
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeQualifiedUserTypeName(sqlType: accountNumber).Should().Be("[sales].[AccountNumber]");
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeCanBeBTreeKey(sqlType: point).Should().BeTrue();
        Validate(point,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[15])).Should().Throw<ArgumentException>();
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeCanBeBTreeKey(sqlType: sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateClrUserDefinedSqlType("dbo","Opaque","Types","Opaque",SqlClrSerializationFormat.UserDefined,-1)).Should().BeFalse();
        sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateClrUserDefinedSqlType("dbo","Opaque","Types",SqlClrSerializationFormat.UserDefined,-1).ClrClassName.Should().Be("Opaque");
        ((Func<SqlType>)(() => sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateAliasSqlType("dbo","Bad",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeXml()))).Should().Throw<ArgumentException>();
        Validate(sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeHierarchyId(),sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[893])).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ScalarAndTableTypes_RoundTripThroughCatalogMetadata()
    {
        var accountNumber = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateAliasSqlType("sales","AccountNumber",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(12),false);
        var point = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateClrUserDefinedSqlType("dbo","Point","SpatialTypes","Samples.Point",SqlClrSerializationFormat.UserDefined,16,isByteOrdered: true,isFixedLength: true,validationMethodName: "ValidatePoint");
        var typedXml = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateTypedXmlSqlType(sql_storage_engine.Exposures.Rows.SqlXmlSchemaCollectionExposure.CreateSqlXmlSchemaCollection("dbo","BusinessSchemas",[InvoiceSchema, CustomerSchema]),SqlXmlContentKind.Document);
        var tableType = sql_storage_engine.Exposures.Catalog.CatalogTableTypeExposure.CreateCatalogTableType("sales", "AccountBatch", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "account", accountNumber, false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "point", point, false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "payload", typedXml, true)], [sql_storage_engine.Exposures.Catalog.CatalogTableTypeIndexExposure.CreateCatalogTableTypeIndex("PK_AccountBatch", true, true, [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)])]);
        var table = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "accounts", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 3 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "account", accountNumber, false)]);
        var expected = sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([table], [], [sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateCatalogScalarType(sqlType: accountNumber), sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateCatalogScalarType(sqlType: point)], [tableType]);
        var actual = sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(expected));
        actual.ScalarTypes.Should().BeEquivalentTo(expected.ScalarTypes);
        actual.TableTypes.Should().BeEquivalentTo(expected.TableTypes);
        actual.Tables.Should().BeEquivalentTo(expected.Tables);
    }

    [Test]
    public void TypedXmlCatalogMetadata_CanExceedLegacyUInt16StringLimit()
    {
        var documentation = new string ('x', 70_000);
        var xsd = $"""
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:annotation><xs:documentation>{documentation}</xs:documentation></xs:annotation>
              <xs:element name="root" type="xs:string" />
            </xs:schema>
            """;
        var type = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateTypedXmlSqlType(sql_storage_engine.Exposures.Rows.SqlXmlSchemaCollectionExposure.CreateSqlXmlSchemaCollection("dbo","LargeSchema",xsd));
        var catalog = sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "documents", 1, new sql_storage_engine.Models.Identifiers.PageId { Value = 2 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", type, false)])], []);
        sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Decode(sql_storage_engine.Exposures.Catalog.CatalogCodecExposure.Encode(catalog)).Tables.Single().Columns.Single().Type.Should().Be(type);
    }

    [Test]
    public async Task PublicCatalogApi_PersistsNamedTypesAcrossReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sql-storage-types-{Guid.NewGuid():N}.db");
        var accountNumber = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateAliasSqlType("sales","AccountNumber",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(12),false);
        try
        {
            await using (var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                await engine.CreateCatalogScalarTypeAsync(accountNumber);
                await engine.CreateCatalogTableTypeAsync("sales", "AccountBatch", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "account", accountNumber, false)]);
                await engine.CreateCatalogTableAsync("accounts", [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "account", accountNumber, false)]);
            }

            await using var reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var resolvedAccountNumber = sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateAliasSqlType("sales","AccountNumber",sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(12, reopened.ReadCatalog().ReadDefaultCollation()),false);
            reopened.ReadCatalog().TryGetScalarType("sales", "AccountNumber", out var scalar).Should().BeTrue();
            scalar!.Definition.Should().Be(resolvedAccountNumber);
            reopened.ReadCatalog().TryGetTableType("sales", "AccountBatch", out var tableType).Should().BeTrue();
            tableType!.Columns.Single().Type.Should().Be(resolvedAccountNumber);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static Action Validate(SqlType type, SqlValue value) => () => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CheckColumnDefinitionValue(columnDefinition: sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"value",type,false),sqlValue: value);
}