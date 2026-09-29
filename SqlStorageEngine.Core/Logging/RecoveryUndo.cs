using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Logging;

/// <summary>Undoes incomplete transactions through previous-LSN chains and writes restartable compensation progress.</summary>
public static class RecoveryUndo
{
    public static async ValueTask<bool> ApplyAsync(IPageStore pageStore, RecoveryAnalysis analysis,
        WriteAheadLog wal, int maximumActions = int.MaxValue, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumActions);
        var records = analysis.Records.ToDictionary(record => record.Lsn);
        var actions = 0;
        foreach (var transaction in analysis.Transactions.Where(pair => pair.Value == TransactionState.Active))
        {
            var current = analysis.Records.Where(record => record.TransactionId == transaction.Key)
                .OrderByDescending(record => record.Lsn.Value).FirstOrDefault();
            var previous = current?.Lsn ?? default;
            while (previous.Value != 0)
            {
                if (!records.TryGetValue(previous, out var record))
                    throw new StorageCorruptionException("Transaction previous-LSN chain references a missing record.");
                if (record.Type == WalRecordType.PageChange)
                {
                    var change = PhysicalPageChangeCodec.Read(record.Payload.Span);
                    ValidateBeforeImage(change);
                    await pageStore.WriteAsync(change.PageId, change.BeforeImage, cancellationToken).ConfigureAwait(false);
                    var compensation = await wal.AppendAsync(transaction.Key, WalRecordType.Compensation,
                        record.PreviousLsn, record.Payload, cancellationToken).ConfigureAwait(false);
                    await wal.FlushThroughAsync(compensation.Lsn, cancellationToken).ConfigureAwait(false);
                    actions++;
                    if (actions >= maximumActions) return false;
                }
                previous = record.PreviousLsn;
            }
            var rollback = await wal.AppendAsync(transaction.Key, WalRecordType.Rollback, current?.Lsn ?? default,
                ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
            await wal.FlushThroughAsync(rollback.Lsn, cancellationToken).ConfigureAwait(false);
        }
        return true;
    }

    private static void ValidateBeforeImage(PhysicalPageChange change)
    {
        PageChecksum.ValidateChecksum(change.BeforeImage.Span, change.BeforeImage.Length);
        PageHeaderCodec.Read(change.BeforeImage.Span).Validate(change.PageId, change.PageType);
    }
}
