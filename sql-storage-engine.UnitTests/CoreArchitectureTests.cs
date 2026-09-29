using System.Reflection;
using SqlStorageEngine.Core;

namespace sql_storage_engine.UnitTests;

public sealed class CoreArchitectureTests
{
    [Test]
    public void CoreHasOnlyNeutralAndFrameworkDependenciesAndExplicitRawApi()
    {
        var core = typeof(PageBackendFactory).Assembly;
        Assert.That(core.GetReferencedAssemblies().Select(a => a.Name),
            Has.All.Matches<string>(n => n == "SqlExecutionEngine.Storage.Abstractions" || n == "Microsoft.Win32.Primitives" || n == "netstandard" || n.StartsWith("System", StringComparison.Ordinal)));
        Assert.That(core.GetExportedTypes().Where(t => t.Namespace == "SqlStorageEngine.Core").Select(t => t.Name),
            Is.EquivalentTo(new[] { "PageBackendFactory", "PageBackendOptions" }));
        Assert.That(core.GetTypes().Select(t => t.Namespace ?? ""),
            Has.None.Matches<string>(n => n.Contains(".Catalog") || n.Contains(".Rows") || n.Contains(".Security")));
        var shape = string.Join("\n", core.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal).Select(t =>
            t.FullName + "\n" + string.Join("\n", t.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(m => m.MemberType + ":" + m).Order(StringComparer.Ordinal))));
        Assert.That(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(shape))),
            Is.EqualTo("38BFDB8CBCF94BB721B78E23B3C64D57A1803609BD0FEA4FED40363ED2216C3C"),
            "Core API growth requires explicit responsibility and compatibility review.");
        var compatibility = typeof(StorageEngine).Assembly;
        var forwarded = compatibility.GetForwardedTypes();
        Assert.That(core.GetExportedTypes().Where(t => t.Namespace != "SqlStorageEngine.Core"), Is.EquivalentTo(forwarded));
        Assert.That(forwarded, Has.None.Matches<Type>(t => t.Assembly != core));
        TestContext.Out.WriteLine($"Core exported: {core.GetExportedTypes().Length}; compatibility defined: {compatibility.GetExportedTypes().Length}; forwarded: {forwarded.Length}");
    }

    [Test]
    public void PhysicalSourceCannotImportSqlPolicyAndCoreCannotGrowProjectEdges()
    {
        var root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
        var directory = Path.Combine(root, "SqlStorageEngine.Core");
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                     .Where(p => !p.Contains("/obj/") && !p.Contains("/bin/")))
        {
            var source = File.ReadAllText(file);
            foreach (var forbidden in new[] { "sql_storage_engine.Catalog", "sql_storage_engine.Rows", "sql_storage_engine.Security", "StorageEngine.Create", "StorageEngine.Open" })
                Assert.That(source, Does.Not.Contain(forbidden), file);
        }
        var project = System.Xml.Linq.XDocument.Load(Path.Combine(directory, "SqlStorageEngine.Core.csproj"));
        Assert.That(project.Descendants("ProjectReference"), Is.Empty);
        Assert.That(project.Descendants("PackageReference").Select(e => (string?)e.Attribute("Include")),
            Is.EqualTo(new[] { "SqlExecutionEngine.Storage.Abstractions" }));
    }
}
