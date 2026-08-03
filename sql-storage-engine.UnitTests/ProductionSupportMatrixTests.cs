using System.Text.Json;
using AwesomeAssertions;

namespace sql_storage_engine.UnitTests;

public sealed class ProductionSupportMatrixTests
{
    [Test]
    public void EverySupportedCombinationHasCompleteExecutableQualificationEvidence()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(DocumentPath("qualification-evidence.json")));
        var combinations = document.RootElement.GetProperty("combinations").EnumerateArray().ToArray();
        combinations.Should().ContainSingle();
        foreach (var combination in combinations)
        {
            combination.GetProperty("id").GetString().Should().NotBeNullOrWhiteSpace();
            combination.GetProperty("command").GetString().Should().Contain("-warnaserror");
            combination.GetProperty("requiredSuites").GetArrayLength().Should().BeGreaterThanOrEqualTo(13);
            foreach (var suite in combination.GetProperty("requiredSuites").EnumerateArray())
                Directory.GetFiles(RepositoryPath("sql-storage-engine.UnitTests"), suite.GetString() + ".cs").Should().ContainSingle();
        }
    }

    [Test]
    public void MatrixPublishesExclusionsGuaranteesCapacitiesAndProductionEnvelope()
    {
        var text = File.ReadAllText(DocumentPath("production-support.md"));
        foreach (var required in new[] { "Network filesystems", "Removable", "Commit succeeds", "Crash recovery",
                     "Offline backup", "Online backup", "Point-in-time restore", "Format upgrade", "Hard ceiling",
                     "production grade", "linux-x64-ext4" })
            text.Should().Contain(required);
    }

    private static string DocumentPath(string name) => RepositoryPath("docs", name);
    private static string RepositoryPath(params string[] parts)
    {
        var root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
        return Path.Combine([root, .. parts]);
    }
}
