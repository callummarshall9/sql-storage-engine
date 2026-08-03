using AwesomeAssertions;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class ShutdownMarkerTests
{
    [Test]
    public async Task WriterStartupMarksRecoveryRequiredDurablyAndCleanShutdownClearsAfterFlush()
    {
        using var path = new DatabasePath();
        await using (var created = await PageDatabase.CreateAsync(path.Value)) { }
        ReadHeader(path.Value).IsCleanShutdown.Should().BeTrue();

        var writer = await PageDatabase.OpenAsync(path.Value);
        ReadHeader(path.Value).IsCleanShutdown.Should().BeFalse();
        await writer.DisposeAsync();

        ReadHeader(path.Value).IsCleanShutdown.Should().BeTrue();
        await using var readOnly = await PageDatabase.OpenAsync(path.Value, DatabaseOpenMode.ReadOnly);
    }

    [Test]
    public async Task ForcedTerminationStateRejectsReadOnlyOpenWithoutModification()
    {
        using var path = new DatabasePath();
        await using (var created = await PageDatabase.CreateAsync(path.Value)) { }
        var writer = await PageDatabase.OpenAsync(path.Value);
        var before = await File.ReadAllBytesAsync(path.Value);

        await ((Func<Task>)(async () => await PageDatabase.OpenAsync(path.Value, DatabaseOpenMode.ReadOnly)))
            .Should().ThrowAsync<RecoveryRequiredException>();

        (await File.ReadAllBytesAsync(path.Value)).Should().Equal(before);
        await writer.DisposeAsync();
    }

    private static DatabaseHeader ReadHeader(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return DatabaseHeaderCodec.Read(bytes.AsSpan(0, PageConstants.DefaultSize));
    }
    private sealed class DatabasePath : IDisposable
    {
        public DatabasePath() => Value = Path.Combine(Path.GetTempPath(), "sql-shutdown-" + Guid.NewGuid().ToString("N") + ".db");
        public string Value { get; }
        public void Dispose() { if (File.Exists(Value)) File.Delete(Value); }
    }
}
