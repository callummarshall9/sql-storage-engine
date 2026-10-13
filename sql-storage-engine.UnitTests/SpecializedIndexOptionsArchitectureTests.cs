using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Exposures.Catalog;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;
public sealed class SpecializedIndexOptionsArchitectureTests
{
    [Test]
    public void JsonPathsSnapshotInputsAndRejectOverlapsMalformedQuotesAndNullSources()
    {
        var paths = new[] { "$.a", "$.b[0 to 2]" };
        var options = CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions(paths);
        paths[0] = "$.changed";
        options.JsonPaths.Should().Equal("$.a", "$.b[0 to 2]");
        ((Action)(() => ((IList<string>)options.JsonPaths)[0] = "$" )).Should().Throw<NotSupportedException>();
        CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions().JsonPaths.Should().Equal("$");
        ((Action)(() => CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions(null!))).Should().Throw<ArgumentException>();
        ((Action)(() => CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions("$.a","$.a.b"))).Should().Throw<ArgumentException>();
        var invalid = ((Action)(() => CatalogSpecializedIndexOptionsExposure.CreateJsonCatalogSpecializedIndexOptions("$.\"bad\\q\""))).Should().Throw<ArgumentException>();
        invalid.Which.InnerException.Should().NotBeNull();
    }

    [Test]
    public void XmlNamespacesSnapshotInputsAndResolvePrefixesDuringPathValidation()
    {
        var namespaces = new Dictionary<string, string> { ["n"] = "urn:original" };
        var options = CatalogSpecializedIndexOptionsExposure.CreateXmlCatalogSpecializedIndexOptions(CatalogXmlIndexKind.Selective, namespaces, "/n:item/@id");
        namespaces["n"] = "urn:changed";
        options.XmlNamespaces["n"].Should().Be("urn:original");
        ((Action)(() => ((IDictionary<string, string>)options.XmlNamespaces)["n"] = "urn:mutated")).Should().Throw<NotSupportedException>();
        ((Action)(() => CatalogSpecializedIndexOptionsExposure.CreateXmlCatalogSpecializedIndexOptions(CatalogXmlIndexKind.Selective,"/missing:item"))).Should().Throw<ArgumentException>();
        ((Action)(() => CatalogSpecializedIndexOptionsExposure.CreateXmlCatalogSpecializedIndexOptions(CatalogXmlIndexKind.Selective))).Should().Throw<ArgumentException>();
        ((Action)(() => CatalogSpecializedIndexOptionsExposure.CreateXmlCatalogSpecializedIndexOptions(kind: CatalogXmlIndexKind.Primary,namespaces: null!,paths: ["/"]))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void ScalarCatalogCreationUsesValidatedSqlDeclarations()
    {
        var alias = SqlTypeExposure.CreateAliasSqlType("dbo","Account",SqlTypeExposure.ReadSqlTypeInt(),false);
        var scalar = SqlTypeExposure.CreateCatalogScalarType(alias);
        scalar.Definition.Should().Be(alias);
        ((Action)(() => SqlTypeExposure.CreateCatalogScalarType(SqlTypeExposure.ReadSqlTypeInt()))).Should().Throw<ArgumentException>();
        ((Action)(() => SqlTypeExposure.CreateCatalogScalarType(new SqlType { Name = SqlTypeName.Int, UserTypeKind = SqlUserTypeKind.Alias }))).Should().Throw<ArgumentException>();
    }
}