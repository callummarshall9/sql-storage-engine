using AwesomeAssertions;
using sql_storage_engine.Security;
using sql_storage_engine.Storage;
using sql_storage_engine.Models;
using sql_storage_engine.Exposures.Transactions;

using sql_storage_engine.Models.Security;

namespace sql_storage_engine.UnitTests;
public sealed class SecurityEventTests
{
    private static StorageAuditRecord Event() => new sql_storage_engine.Models.Security.StorageAuditRecord{OperationId=Guid.NewGuid(),CorrelationId=Guid.NewGuid(),Principal=null,ObjectId=null,Action=StoragePermissionAction.Select,Kind=StorageAuditKind.Denied,Revision=1};
    [Test]
    public async Task DeniedEventIsDurableDeduplicatedAndSurvivesRestart()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var record = Event();
        await db.Engine.ReadSecurity().AppendEventAsync(record);
        await db.Engine.ReadSecurity().AppendEventAsync(record);
        Func<Task> conflict = async () => await db.Engine.ReadSecurity().AppendEventAsync(record with { Revision = 2 });
        await conflict.Should().ThrowAsync<ArgumentException>();
        await db.ReopenAsync();
        (await db.Engine.ReadSecurity().ReadEventsAsync()).Should().Equal(record);
        Func<Task> invalid = async () => await db.Engine.ReadSecurity().AppendEventAsync(record with { Kind = StorageAuditKind.Mutation });
        await invalid.Should().ThrowAsync<ArgumentException>();
    }

    [TestCase("security-event-before-write", false)]
    [TestCase("security-event-before-publish", false)]
    [TestCase("security-event-after-publish", true)]
    [TestCase("security-event-durable", true)]
    public async Task FailedAcknowledgementQuarantinesAppendsAndReopenResolvesPresence(string phase, bool present)
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var record = Event();
        db.Engine.WriteTransactionObserver(observer: stage =>
        {
            if (stage == phase)
                throw new IOException("sensitive detail");
        });
        Func<Task> append = async () => await db.Engine.ReadSecurity().AppendEventAsync(record);
        var failure = await append.Should().ThrowAsync<StorageAuditException>();
        failure.Which.OperationId.Should().Be(record.OperationId);
        failure.Which.Message.Should().NotContain("sensitive");
        db.Engine.WriteTransactionObserver(observer: null);
        await append.Should().ThrowAsync<StorageAuditException>();
        await db.ReopenAsync();
        (await db.Engine.ReadSecurity().ReadEventsAsync()).Any(a => a.OperationId == record.OperationId).Should().Be(present);
        await append();
        (await db.Engine.ReadSecurity().ReadEventsAsync()).Should().Equal(record);
    }

    [Test]
    public async Task CancellationAndDisposedAccessHaveNoAppend()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var record = Event();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Func<Task> append = async () => await db.Engine.ReadSecurity().AppendEventAsync(record, canceled.Token);
        await append.Should().ThrowAsync<OperationCanceledException>();
        (await db.Engine.ReadSecurity().ReadEventsAsync()).Should().BeEmpty();
        var security = db.Engine.ReadSecurity();
        await db.Engine.DisposeAsync();
        Func<Task> disposed = async () => await security.AppendEventAsync(record);
        await disposed.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task DenialRequiresAbortedRootAndCorruptEventsFailClosed()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var record = Event();
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        Func<Task> early = async () => await db.Engine.ReadSecurity().AppendEventAsync(record);
        await early.Should().ThrowAsync<InvalidOperationException>();
        await root.RollbackAsync();
        await early();
        var path = db.Path + ".security-events";
        var bytes = await File.ReadAllBytesAsync(path);
        bytes[^1] ^= 1;
        await File.WriteAllBytesAsync(path, bytes);
        Func<Task> read = async () => await db.Engine.ReadSecurity().ReadEventsAsync();
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
        await File.WriteAllBytesAsync(path, [..db.Engine.ReadDatabaseId().Value.ToByteArray(), ..System.Security.Cryptography.SHA256.HashData(payload), ..payload]);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await db.Engine.ReadSecurity().AppendEventAsync(Event());
        watch.Stop();
        TestContext.Out.WriteLine($"4096-event append: {new FileInfo(path).Length} retained bytes; {watch.Elapsed.TotalMilliseconds:F3} ms.");
        Func<Task> over = async () => await db.Engine.ReadSecurity().AppendEventAsync(Event());
        await over.Should().ThrowAsync<InvalidOperationException>();
        (await db.Engine.ReadSecurity().ReadEventsAsync()).Count.Should().Be(4096);
        await db.Engine.ReadSecurity().AppendEventAsync(records[0]);
        (await db.Engine.ReadSecurity().ReadEventsAsync()).Count.Should().Be(4096);
    }
}