// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class ShutdownMarkerTests
{
    [Test]
    public async Task WriterStartupMarksRecoveryRequiredDurablyAndCleanShutdownClearsAfterFlush()
    {
        using var path = new DatabasePath();
        await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure created = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path.Value))
        {
        }

        ReadHeader(path.Value).IsCleanShutdown.Should().BeTrue();
        sql_storage_engine.Exposures.Pages.PageDatabaseExposure writer = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path.Value);
        ReadHeader(path.Value).IsCleanShutdown.Should().BeFalse();
        await writer.DisposeAsync();
        ReadHeader(path.Value).IsCleanShutdown.Should().BeTrue();
        await using sql_storage_engine.Exposures.Pages.PageDatabaseExposure readOnly = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path.Value,DatabaseOpenMode.ReadOnly);
    }

    [Test]
    public async Task ForcedTerminationStateRejectsReadOnlyOpenWithoutModification()
    {
        using var path = new DatabasePath();
        await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure created = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path.Value))
        {
        }

        sql_storage_engine.Exposures.Pages.PageDatabaseExposure writer = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path.Value);
        var before = await File.ReadAllBytesAsync(path.Value);
        await ((Func<Task>)(async () => await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path.Value,DatabaseOpenMode.ReadOnly))).Should().ThrowAsync<RecoveryRequiredException>();
        (await File.ReadAllBytesAsync(path.Value)).Should().Equal(before);
        await writer.DisposeAsync();
    }

    private static DatabaseHeader ReadHeader(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.ReadDatabaseHeader(bytes.AsSpan(0, sql_storage_engine.Models.Pages.PageConstants.DefaultSize));
    }

    private sealed class DatabasePath : IDisposable
    {
        public DatabasePath() => Value = Path.Combine(Path.GetTempPath(), "sql-shutdown-" + Guid.NewGuid().ToString("N") + ".db");
        public string Value { get; }

        public void Dispose()
        {
            if (File.Exists(Value))
                File.Delete(Value);
        }
    }
}