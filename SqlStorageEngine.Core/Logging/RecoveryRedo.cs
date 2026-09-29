using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

/// <summary>Replays committed full-page changes when their LSN is newer than the stored page.</summary>
public static class RecoveryRedo
{
    public static async ValueTask ApplyAsync(IPageStore pageStore, RecoveryAnalysis analysis,
        CancellationToken cancellationToken = default)
    {
        foreach (var record in analysis.Records.Where(record => record.Type == WalRecordType.PageChange &&
                     analysis.Transactions.GetValueOrDefault(record.TransactionId) == Transactions.TransactionState.Committed))
        {
            var change = PhysicalPageChangeCodec.Read(record.Payload.Span);
            var after = change.AfterImage.ToArray();
            ValidateImage(after, change, record.Lsn);
            var current = new byte[pageStore.PageSize];
            await pageStore.ReadAsync(change.PageId, current, cancellationToken).ConfigureAwait(false);
            try
            {
                PageChecksum.ValidateChecksum(current, pageStore.PageSize);
                var header = PageHeaderCodec.Read(current);
                header.Validate(change.PageId, change.PageType);
                if (header.PageLogSequenceNumber.Value >= record.Lsn.Value) continue;
            }
            catch (StorageCorruptionException)
            {
                // A verified logged full-page image is the only source allowed to repair a torn/corrupt page.
            }
            await pageStore.WriteAsync(change.PageId, after, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ValidateImage(byte[] image, PhysicalPageChange change, LogSequenceNumber expectedLsn)
    {
        PageChecksum.ValidateChecksum(image, image.Length);
        var header = PageHeaderCodec.Read(image);
        header.Validate(change.PageId, change.PageType);
        if (header.PageLogSequenceNumber != expectedLsn)
            throw new StorageCorruptionException("Logged after-image LSN does not match its WAL record.");
    }
}
