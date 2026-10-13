using sql_storage_engine.Exposures.Logging;
using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;
public sealed class DurableTransactionTests
{
    [Test]
    public async Task CommitReturnsOnlyAfterCommitRecordIsFlushed()
    {
        var device = new WriteAheadLogTests.MemoryWalDevice();
        var wal = await sql_storage_engine.Exposures.Logging.WriteAheadLogExposure.OpenAsync(device);
        var transaction = wal.CreateWriteAheadLogExposureTransaction(new TransactionId { Value = 1 });
        await transaction.AppendChangeAsync(new byte[] { 1 });
        await transaction.CommitAsync();
        transaction.ReadTransactionState().Should().Be(TransactionState.Committed);
        wal.ReadDurableLsn().Value.Should().BeGreaterThan(0);
        WalReadResultExposure.ReadWalReadResult(device.Bytes).Records[^1].Type.Should().Be(WalRecordType.Commit);
    }

    [Test]
    public async Task FlushFailureDoesNotReportSuccessfulCommitOrAllowMutation()
    {
        var device = new WriteAheadLogTests.MemoryWalDevice
        {
            FailFlush = true
        };
        var transaction = (await WriteAheadLogExposure.OpenAsync(device)).CreateWriteAheadLogExposureTransaction(new TransactionId { Value = 1 });
        await ((Func<Task>)(async () => await transaction.CommitAsync())).Should().ThrowAsync<IOException>();
        transaction.ReadTransactionState().Should().Be(TransactionState.Failed);
        await ((Func<Task>)(async () => await transaction.AppendChangeAsync(new byte[] { 1 }))).Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task LostClientResponseAfterFlushLeavesTransactionDurablyCommitted()
    {
        var device = new WriteAheadLogTests.MemoryWalDevice();
        var transaction = (await WriteAheadLogExposure.OpenAsync(device)).CreateWriteAheadLogExposureTransaction(new TransactionId { Value = 1 });
        await ((Func<Task>)(async () => await transaction.CommitAsync(() => ValueTask.FromException(new IOException("response lost"))))).Should().ThrowAsync<IOException>();
        transaction.ReadTransactionState().Should().Be(TransactionState.Committed);
        WalReadResultExposure.ReadWalReadResult(device.Bytes).Records.Single().Type.Should().Be(WalRecordType.Commit);
    }
}