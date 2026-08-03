using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class DurableTransactionTests
{
    [Test]
    public async Task CommitReturnsOnlyAfterCommitRecordIsFlushed()
    {
        var device = new WriteAheadLogTests.MemoryWalDevice();
        var wal = await WriteAheadLog.OpenAsync(device);
        var transaction = new DurableTransaction(new TransactionId(1), wal);
        await transaction.AppendChangeAsync(new byte[] { 1 });
        await transaction.CommitAsync();
        transaction.State.Should().Be(TransactionState.Committed);
        wal.DurableLsn.Value.Should().BeGreaterThan(0);
        WalFormat.ReadRecords(device.Bytes).Records[^1].Type.Should().Be(WalRecordType.Commit);
    }

    [Test]
    public async Task FlushFailureDoesNotReportSuccessfulCommitOrAllowMutation()
    {
        var device = new WriteAheadLogTests.MemoryWalDevice { FailFlush = true };
        var transaction = new DurableTransaction(new TransactionId(1), await WriteAheadLog.OpenAsync(device));
        await ((Func<Task>)(async () => await transaction.CommitAsync())).Should().ThrowAsync<IOException>();
        transaction.State.Should().Be(TransactionState.Failed);
        await ((Func<Task>)(async () => await transaction.AppendChangeAsync(new byte[] { 1 }))).Should()
            .ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task LostClientResponseAfterFlushLeavesTransactionDurablyCommitted()
    {
        var device = new WriteAheadLogTests.MemoryWalDevice();
        var transaction = new DurableTransaction(new TransactionId(1), await WriteAheadLog.OpenAsync(device));
        await ((Func<Task>)(async () => await transaction.CommitAsync(
            () => ValueTask.FromException(new IOException("response lost"))))).Should().ThrowAsync<IOException>();
        transaction.State.Should().Be(TransactionState.Committed);
        WalFormat.ReadRecords(device.Bytes).Records.Single().Type.Should().Be(WalRecordType.Commit);
    }
}
