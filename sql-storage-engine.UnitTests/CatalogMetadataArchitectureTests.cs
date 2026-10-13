using System.Numerics;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Exposures.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;

using sql_storage_engine.Models.Rows;
namespace sql_storage_engine.UnitTests;
public sealed class CatalogMetadataArchitectureTests
{
    [Test]
    public void BTreeOptionsMaterializeOnceAndRetainAnIndependentReadOnlySnapshot()
    {
        var enumerations = 0;
        var source = new List<ColumnId>
        {
            new sql_storage_engine.Models.Identifiers.ColumnId
            {
                Value = 1
            },
            new sql_storage_engine.Models.Identifiers.ColumnId
            {
                Value = 2
            }
        };
        IEnumerable<ColumnId> Enumerate()
        {
            enumerations++;
            foreach (var column in source)
                yield return column;
        }

        var options = CatalogBTreeIndexOptionsExposure.CreateCatalogBTreeIndexOptions(columnIds: Enumerate());
        source.Clear();
        enumerations.Should().Be(1);
        options.IncludedColumns.Should().Equal(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 });
        options.IncludedColumnSource.Should().BeNull();
        ((Action)(() => ((IList<ColumnId>)options.IncludedColumns).Clear())).Should().Throw<NotSupportedException>();
    }

    [Test]
    public void DirectlyInitializedIdentityAndEncryptionAreValidatedAtColumnAdmission()
    {
        var identity = new CatalogIdentity
        {
            Seed = BigInteger.One,
            Increment = BigInteger.Zero
        };
        ((Action)(() => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false, catalogIdentity: identity))).Should().Throw<ArgumentException>().Which.InnerException.Should().BeAssignableTo<ArgumentOutOfRangeException>();
        var encryption = new CatalogColumnEncryption
        {
            KeyName = "key",
            EncryptionType = CatalogEncryptionType.Deterministic,
            Algorithm = "invalid"
        };
        ((Action)(() => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false, catalogColumnEncryption: encryption))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void DirectlyInitializedIndexColumnsAndChecksAreValidatedAtCatalogAdmission()
    {
        var columns = new[]
        {
            new CatalogIndexedColumn
            {
                ColumnId = new sql_storage_engine.Models.Identifiers.ColumnId
                {
                    Value = 1
                },
                Direction = (SortDirection)255,
                NullSortOrder = NullSortOrder.Last
            }
        };
        ((Action)(() => sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new IndexId { Value = 1 },name: "ix",tableId: new TableId { Value = 1 },pageId: new PageId { Value = 2 },isUnique: false,columns: columns))).Should().Throw<ArgumentException>();
        var checks = new[]
        {
            new CatalogCheckConstraint
            {
                Name = "check",
                Expression = " "
            }
        };
        ((Action)(() => sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new TableId { Value = 1 }, "table", 1, new PageId { Value = 2 }, [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false)], checkConstraints: checks))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void IdentityOverloadsPreserveLargeValuesAndLegacyReplicationFlags()
    {
        var seed = BigInteger.One << 100;
        CatalogIdentityExposure.CreateCatalogIdentity(seed,BigInteger.MinusOne).IsNotForReplication.Should().BeFalse();
        var identity = CatalogIdentityExposure.CreateCatalogIdentity(seed,BigInteger.MinusOne,null);
        identity.Seed.Should().Be(seed);
        identity.Increment.Should().Be(BigInteger.MinusOne);
        identity.IsNotForReplication.Should().BeNull();
    }

    [Test]
    public void AssemblyImagesHaveValueEqualityAndIndependentSnapshots()
    {
        byte[] image = [0, 1, 127, 128, 255];
        var assembly = CatalogAssemblyExposure.CreateCatalogAssembly("assembly",image,"1.2.3.4",publicKeyToken: "ABCDEF0123456789");
        image[0] = 99;
        assembly.PublicKeyToken.Should().Be("abcdef0123456789");
        CatalogAssemblyExposure.ReadCatalogAssemblyImage(assembly).ToArray().Should().Equal(0, 1, 127, 128, 255);
        var equal = CatalogAssemblyExposure.CreateCatalogAssembly("assembly",[0, 1, 127, 128, 255],"1.2.3.4",publicKeyToken: "abcdef0123456789");
        assembly.Should().Be(equal);
        new HashSet<CatalogAssembly> { assembly }.Contains(equal).Should().BeTrue();
        var copy = CatalogAssemblyExposure.ReadCatalogAssemblyImage(assembly).ToArray();
        copy[1] = 42;
        CatalogAssemblyExposure.ReadCatalogAssemblyImage(assembly).Span[1].Should().Be(1);
        var invalid = assembly with { ImageContent = "\u0100" };
        ((Action)(() => sql_storage_engine.Exposures.Catalog.CatalogDefinitionExposure.CreateCatalogDefinition([], [], assemblies: [invalid]))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void FullTextMetadataRoundTripsAndRetainsMalformedNumericCause()
    {
        var options = CatalogFullTextIndexOptionsExposure.CreateCatalogFullTextIndexOptions("Latin1_General_100_CI_AI",maximumTokensPerDocument: 128,maximumTokenLength: 64);
        var fields = CatalogFullTextIndexOptionsExposure.EncodeCatalogFullTextIndexOptions(options);
        CatalogFullTextIndexOptionsExposure.DecodeCatalogFullTextIndexOptions(fields).Should().Be(options);
        var malformed = fields.ToArray();
        malformed[3] = "65536";
        var failure = ((Func<CatalogFullTextIndexOptions>)(() => CatalogFullTextIndexOptionsExposure.DecodeCatalogFullTextIndexOptions(malformed))).Should().Throw<ArgumentException>().Which;
        failure.InnerException!.InnerException.Should().BeOfType<OverflowException>();
        var invalid = options with { MaximumTokensPerDocument = 0 };
        ((Action)(() => sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateFullTextCatalogSpecializedIndexOptions(invalid))).Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maximumTokensPerDocument");
    }

    [Test]
    public void RawGraphMetadataIsValidatedBeforeTableAdmission()
    {
        var graph = CatalogGraphTableExposure.CreateCatalogGraphTableNode(1,2);
        CatalogGraphTableExposure.ReadSupportsOutgoingTraversal(graph).Should().BeFalse();
        var invalid = graph with { FromNodeTableId = new TableId { Value = 3 } };
        ((Action)(() => sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new TableId { Value = 1 }, "table", 1, new PageId { Value = 2 },
            [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeBigInt(), false)], catalogGraphTable: invalid))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void QualifiedNamesValidateRawDataAndKeepDiagnosticFormatting()
    {
        var name = CatalogTableNameExposure.CreateCatalogTableName("db","schema","table");
        CatalogTableNameExposure.FormatCatalogTableName(name).Should().Be("[db].[schema].[table]");
        var invalid = name with { DatabaseName = " " };
        ((Func<string>)(() => CatalogTableNameExposure.FormatCatalogTableName(invalid))).Should().Throw<ArgumentException>().WithParameterName("databaseName");
    }

    [Test]
    public void ColumnIdsKeepJsonShapeDictionaryEqualityAndInvariantFormatting()
    {
        var columnId = new ColumnId { Value = ulong.MaxValue };
        var json = System.Text.Json.JsonSerializer.Serialize(columnId);
        json.Should().Be("{\"Value\":18446744073709551615}");
        System.Text.Json.JsonSerializer.Deserialize<ColumnId>(json).Should().Be(columnId);
        new Dictionary<ColumnId, int> { [columnId] = 7 }[new ColumnId { Value = ulong.MaxValue }].Should().Be(7);
        sql_storage_engine.Exposures.Identifiers.IdentifierDisplayExposure.FormatColumnId(columnId).Should().Be("column:18446744073709551615");
    }
}