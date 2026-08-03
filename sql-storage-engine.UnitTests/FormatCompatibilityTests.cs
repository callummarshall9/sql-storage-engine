using AwesomeAssertions;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class FormatCompatibilityTests
{
    [Test]
    public async Task EverySupportedFixtureOpensQueriesAndPassesIntegrity()
    {
        using var output = new TempFile();
        var result = await FormatCompatibility.OpenAsync(FixturePath(), output.Path);
        result.Header.FormatVersion.Should().Be(DatabaseHeader.CurrentFormatVersion);
        result.Header.DatabaseId.Value.Should().Be(Guid.Parse("12345678-1234-5678-9abc-def012345678"));
        result.Integrity.IsHealthy.Should().BeTrue();
        result.Rows.Should().Equal("items:1:alpha", "items:2:beta");
        result.WalRecords.Should().HaveCount(2);
    }

    [Test]
    public async Task UnknownFutureVersionIsRejectedWithoutDestinationModification()
    {
        using var fixture = new TempFile(); using var destination = new TempFile(create: true);
        var json = await File.ReadAllTextAsync(FixturePath());
        await File.WriteAllTextAsync(fixture.Path, json.Replace("\"FormatVersion\": 1", "\"FormatVersion\": 99", StringComparison.Ordinal));
        var before = await File.ReadAllBytesAsync(destination.Path);
        await ((Func<Task>)(async () => await FormatCompatibility.OpenAsync(fixture.Path, destination.Path)))
            .Should().ThrowAsync<UnsupportedDatabaseVersionException>();
        (await File.ReadAllBytesAsync(destination.Path)).Should().Equal(before);
    }

    [Test]
    public async Task FixtureUpgradePreparation_IsIdempotentAndRestartable()
    {
        using var output = new TempFile();
        await FormatCompatibility.OpenAsync(FixturePath(), output.Path);
        var first = await File.ReadAllBytesAsync(output.Path);
        await FormatCompatibility.OpenAsync(FixturePath(), output.Path);
        (await File.ReadAllBytesAsync(output.Path)).Should().Equal(first);
    }

    private static string FixturePath() => Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFixtures", "format-v1.json");
    private sealed class TempFile : IDisposable
    {
        public TempFile(bool create = false) { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sql-format-" + Guid.NewGuid().ToString("N")); if (create) File.WriteAllText(Path, "unchanged"); }
        public string Path { get; }
        public void Dispose() { if (File.Exists(Path)) File.Delete(Path); }
    }
}
