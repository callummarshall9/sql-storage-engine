namespace sql_storage_engine;

public sealed class StorageTransactionOptions
{
    public const long MaximumSupportedJournalBytes = 256L * 1024 * 1024;
    public StorageTransactionOptions(TimeSpan timeout, StorageTransactionIsolation isolation = StorageTransactionIsolation.Serializable,
        long maximumJournalBytes = MaximumSupportedJournalBytes)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (isolation != StorageTransactionIsolation.Serializable) throw new ArgumentOutOfRangeException(nameof(isolation));
        if (maximumJournalBytes <= 0 || maximumJournalBytes > MaximumSupportedJournalBytes) throw new ArgumentOutOfRangeException(nameof(maximumJournalBytes));
        Timeout = timeout; Isolation = isolation; MaximumJournalBytes = maximumJournalBytes;
    }
    public TimeSpan Timeout { get; }
    public StorageTransactionIsolation Isolation { get; }
    public long MaximumJournalBytes { get; }
}
