using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;

namespace sql_storage_engine.UnitTests;

public sealed class ExplicitTransactionCrashTests
{
    [TestCase("security-event-before-publish")]
    [TestCase("security-event-after-publish")]
    [TestCase("security-active")]
    [TestCase("security-committed")]
    [TestCase("active")]
    [TestCase("nested")]
    [TestCase("committed")]
    [TestCase("savepoint-active")]
    [TestCase("savepoint-rewrite")]
    [TestCase("savepoint-committed")]
    public async Task AbruptProcessExitRecoversEntireRootAndCatalog(string phase)
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        await database.Engine.DisposeAsync();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var probe = Path.Combine(root, "tests/TransactionCrashProbe/bin", configuration, "net10.0/TransactionCrashProbe.dll");
        var start = new ProcessStartInfo("dotnet") { RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add(probe); start.ArgumentList.Add(database.Path); start.ArgumentList.Add(phase);
        using var child = Process.Start(start)!;
        try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
        finally { if (!child.HasExited) child.Kill(true); }
        child.ExitCode.Should().Be(42, await child.StandardError.ReadToEndAsync());
        var identity = JsonSerializer.Deserialize<StorageTransactionIdentity>(await File.ReadAllTextAsync(database.Path + ".probe-identity"));
        await database.ReopenAsync();
        var expected = phase is "committed" or "savepoint-committed" or "security-committed" ? StorageTransactionState.Committed : StorageTransactionState.Aborted;
        (await database.Engine.ResolveTransactionAsync(identity)).State.Should().Be(expected);
        database.Engine.Catalog.Tables.Any(table => table.Name == "crash_created").Should().Be(phase is "committed" or "savepoint-committed" or "security-committed");
        (await database.BalancesAsync()).Should().Equal(100, 100);
        Directory.GetFiles(Path.GetDirectoryName(database.Path)!, "*.savepoint-*").Should().BeEmpty();
        database.Engine.Catalog.Tables.Any(table => table.Name == "savepoint_later").Should().BeFalse();
        if (phase.StartsWith("security", StringComparison.Ordinal))
        {
            (await database.Engine.Security.ReadAsync()).Principals.Count.Should().Be(phase == "security-committed" ? 1 : 0);
            (await database.Engine.Security.ReadAsync()).MutationAudit.Count.Should().Be(phase == "security-committed" ? 1 : 0);
            (await database.Engine.Security.ReadEventsAsync()).Count.Should().Be(phase == "security-event-before-publish" ? 0 : 1);
        }
        File.Exists(database.Path + ".statement-undo").Should().BeFalse();
        File.Exists(database.Path + ".transaction-undo").Should().BeFalse();
    }
}
