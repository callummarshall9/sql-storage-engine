using AwesomeAssertions;
using sql_storage_engine.Security;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class SecurityEventTests
{
    private static StorageAuditRecord Event() => new(Guid.NewGuid(), Guid.NewGuid(), null, null,
        StoragePermissionAction.Select, StorageAuditKind.Denied, 1);

    [Test]
    public async Task DeniedEventIsDurableDeduplicatedAndSurvivesRestart()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); var record = Event();
        await db.Engine.Security.AppendEventAsync(record);
        await db.Engine.Security.AppendEventAsync(record);
        Func<Task> conflict = async () => await db.Engine.Security.AppendEventAsync(record with { Revision = 2 });
        await conflict.Should().ThrowAsync<ArgumentException>();
        await db.ReopenAsync();
        (await db.Engine.Security.ReadEventsAsync()).Should().Equal(record);
        Func<Task> invalid = async () => await db.Engine.Security.AppendEventAsync(record with { Kind = StorageAuditKind.Mutation });
        await invalid.Should().ThrowAsync<ArgumentException>();
    }

    [TestCase("security-event-before-write", false)]
    [TestCase("security-event-before-publish", false)]
    [TestCase("security-event-after-publish", true)]
    [TestCase("security-event-durable", true)]
    public async Task FailedAcknowledgementQuarantinesAppendsAndReopenResolvesPresence(string phase, bool present)
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); var record = Event();
        db.Engine.TransactionObserver = stage => { if (stage == phase) throw new IOException("sensitive detail"); };
        Func<Task> append = async () => await db.Engine.Security.AppendEventAsync(record);
        var failure = await append.Should().ThrowAsync<StorageAuditException>();
        failure.Which.OperationId.Should().Be(record.OperationId);
        failure.Which.Message.Should().NotContain("sensitive");
        db.Engine.TransactionObserver = null;
        await append.Should().ThrowAsync<StorageAuditException>();
        await db.ReopenAsync();
        (await db.Engine.Security.ReadEventsAsync()).Any(a => a.OperationId == record.OperationId).Should().Be(present);
        await append();
        (await db.Engine.Security.ReadEventsAsync()).Should().Equal(record);
    }

    [Test]
    public async Task CancellationAndDisposedAccessHaveNoAppend()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); var record = Event();
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Func<Task> append = async () => await db.Engine.Security.AppendEventAsync(record, canceled.Token);
        await append.Should().ThrowAsync<OperationCanceledException>();
        (await db.Engine.Security.ReadEventsAsync()).Should().BeEmpty();
        var security = db.Engine.Security; await db.Engine.DisposeAsync();
        Func<Task> disposed = async () => await security.AppendEventAsync(record);
        await disposed.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task DenialRequiresAbortedRootAndCorruptEventsFailClosed()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); var record = Event();
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        Func<Task> early = async () => await db.Engine.Security.AppendEventAsync(record);
        await early.Should().ThrowAsync<InvalidOperationException>();
        await root.RollbackAsync(); await early();
        var path = db.Path + ".security-events";
        var bytes = await File.ReadAllBytesAsync(path); bytes[^1] ^= 1; await File.WriteAllBytesAsync(path, bytes);
        Func<Task> read = async () => await db.Engine.Security.ReadEventsAsync();
        await read.Should().ThrowAsync<StorageFormatException>();
        await early.Should().ThrowAsync<StorageFormatException>();
    }

    [Test]
    public async Task EventRetentionBoundaryRejectsOneOverWithoutEviction()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var records = Enumerable.Range(0, 4095).Select(_ => Event()).ToArray();
        var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(records);
        var path = db.Path + ".security-events";
        await File.WriteAllBytesAsync(path, [.. db.Engine.DatabaseId.Value.ToByteArray(), .. System.Security.Cryptography.SHA256.HashData(payload), .. payload]);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await db.Engine.Security.AppendEventAsync(Event());
        watch.Stop();
        TestContext.Out.WriteLine($"4096-event append: {new FileInfo(path).Length} retained bytes; {watch.Elapsed.TotalMilliseconds:F3} ms.");
        Func<Task> over = async () => await db.Engine.Security.AppendEventAsync(Event());
        await over.Should().ThrowAsync<InvalidOperationException>();
        (await db.Engine.Security.ReadEventsAsync()).Count.Should().Be(4096);
        await db.Engine.Security.AppendEventAsync(records[0]);
        (await db.Engine.Security.ReadEventsAsync()).Count.Should().Be(4096);
    }
}
