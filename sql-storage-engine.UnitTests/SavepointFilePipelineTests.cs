using AwesomeAssertions;
using sql_storage_engine.Brokers.Directories;
using sql_storage_engine.Services.Foundations.TransactionDirectories;
using sql_storage_engine.Services.Processings.SavepointFiles;

namespace sql_storage_engine.UnitTests;
public sealed class SavepointFilePipelineTests
{
    [Test]
    public void GeneratedPathsPreserveTheGivenDatabasePathAndUseUniqueIdentities()
    {
        const string databasePath = "relative-database.db";
        var prefix = databasePath + ".savepoint-";
        var service = CreateService();
        var paths = Enumerable.Range(0, 32).Select(_ => service.GenerateSavepointPath(databasePath)).ToArray();
        paths.Distinct(StringComparer.Ordinal).Should().HaveCount(32);
        foreach (var path in paths)
        {
            path.Should().StartWith(prefix);
            Guid.TryParseExact(path[prefix.Length..], "N", out _).Should().BeTrue();
        }
    }

    [Test]
    public void CleanupRemovesOnlyMatchingSavepointFilesAndCanBeRepeated()
    {
        var directory = Path.Combine(Path.GetTempPath(), "savepoint-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var databasePath = Path.Combine(directory, "accounts.db");
            var owned = new[]
            {
                databasePath + ".savepoint-first",
                databasePath + ".savepoint-second"
            };
            var retained = new[]
            {
                databasePath,
                databasePath + ".transaction-undo",
                Path.Combine(directory, "accounts.db2.savepoint-other"),
                Path.Combine(directory, "ACCOUNTS.DB.savepoint-case")
            };
            foreach (var path in owned.Concat(retained))
                File.WriteAllText(path, "content");
            var matchingDirectory = databasePath + ".savepoint-directory";
            Directory.CreateDirectory(matchingDirectory);
            var service = CreateService();
            service.DeleteAllSavepointFiles(databasePath);
            service.DeleteAllSavepointFiles(databasePath);
            owned.Should().OnlyContain(path => !File.Exists(path));
            retained.Should().OnlyContain(path => File.Exists(path));
            Directory.Exists(matchingDirectory).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ITransactionDirectorySavepointProcessingService CreateService() => new TransactionDirectorySavepointProcessingService(new TransactionDirectoryService(new TransactionDirectoryBroker()));
}