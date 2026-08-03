using sql_storage_engine.Storage;

namespace sql_storage_engine.Diagnostics;

/// <summary>Validated capacity and traversal limits with documented engine hard maxima.</summary>
public sealed record StorageLimits
{
    public const int MaximumBufferFrames = 1_000_000;
    public const int MaximumRowBytes = 16 * 1024 * 1024;
    public const int MaximumKeyBytes = ushort.MaxValue;
    public const long MaximumValueBytes = 64L * 1024 * 1024;
    public const int MaximumOverflowPages = 8192;
    public const int MaximumTransactionSeconds = 86400;
    public const long MaximumUndoBytes = 256L * 1024 * 1024;
    public const int MaximumPinsPerTransaction = 65536;
    public const int MaximumScanPages = 1_000_000;
    public const int MaximumConcurrentTransactions = 65536;

    public StorageLimits(int bufferFrames = 1024, int rowBytes = 1024 * 1024, int keyBytes = 4096,
        long valueBytes = 16L * 1024 * 1024, int overflowPages = 2048, int transactionSeconds = 300,
        long undoBytes = 64L * 1024 * 1024, int pinsPerTransaction = 1024, int scanPages = 8192,
        int concurrentTransactions = 1024)
    {
        BufferFrames = Valid(bufferFrames, MaximumBufferFrames, nameof(bufferFrames));
        RowBytes = Valid(rowBytes, MaximumRowBytes, nameof(rowBytes));
        KeyBytes = Valid(keyBytes, MaximumKeyBytes, nameof(keyBytes));
        ValueBytes = Valid(valueBytes, MaximumValueBytes, nameof(valueBytes));
        OverflowPages = Valid(overflowPages, MaximumOverflowPages, nameof(overflowPages));
        TransactionDuration = TimeSpan.FromSeconds(Valid(transactionSeconds, MaximumTransactionSeconds, nameof(transactionSeconds)));
        UndoBytes = Valid(undoBytes, MaximumUndoBytes, nameof(undoBytes));
        PinsPerTransaction = Valid(pinsPerTransaction, MaximumPinsPerTransaction, nameof(pinsPerTransaction));
        ScanPages = Valid(scanPages, MaximumScanPages, nameof(scanPages));
        ConcurrentTransactions = Valid(concurrentTransactions, MaximumConcurrentTransactions, nameof(concurrentTransactions));
    }
    public int BufferFrames { get; }
    public int RowBytes { get; }
    public int KeyBytes { get; }
    public long ValueBytes { get; }
    public int OverflowPages { get; }
    public TimeSpan TransactionDuration { get; }
    public long UndoBytes { get; }
    public int PinsPerTransaction { get; }
    public int ScanPages { get; }
    public int ConcurrentTransactions { get; }
    private static int Valid(int value, int maximum, string name) => checked((int)Valid((long)value, maximum, name));
    private static long Valid(long value, long maximum, string name)
    { if (value <= 0 || value > maximum) throw new ArgumentOutOfRangeException(name, $"Value must be between 1 and {maximum}."); return value; }
}

/// <summary>Checks file-controlled lengths before allocation and owns bounded transaction and pin registrations.</summary>
public sealed class StorageResourceLimiter(StorageLimits limits)
{
    private readonly CapacityCounter _transactions = new();
    private readonly CapacityCounter _pins = new();
    public StorageLimits Limits { get; } = limits ?? throw new ArgumentNullException(nameof(limits));
    public int ActiveTransactions => _transactions.Value;
    public int ActivePins => _pins.Value;
    public IDisposable AcquireTransaction() => Acquire(_transactions, Limits.ConcurrentTransactions, "CONCURRENT_TRANSACTIONS");
    public IDisposable AcquirePin() => Acquire(_pins, Limits.PinsPerTransaction, "TRANSACTION_PINS");
    public void ValidateRowLength(long length) => ValidateLength(length, Limits.RowBytes, "ROW_BYTES");
    public void ValidateKeyLength(long length) => ValidateLength(length, Limits.KeyBytes, "KEY_BYTES");
    public void ValidateValueLength(long length) => ValidateLength(length, Limits.ValueBytes, "VALUE_BYTES");
    public void ValidateOverflowPages(long count) => ValidateLength(count, Limits.OverflowPages, "OVERFLOW_PAGES");
    public void ValidateUndoBytes(long length) => ValidateLength(length, Limits.UndoBytes, "UNDO_BYTES");
    public void ValidateScanPages(long count) => ValidateLength(count, Limits.ScanPages, "SCAN_PAGES");
    private static void ValidateLength(long value, long limit, string code)
    { if (value < 0 || value > limit) throw new StorageResourceExhaustedException($"{code} limit {limit} exceeded by {value}."); }
    private static IDisposable Acquire(CapacityCounter counter, int limit, string code)
    {
        var current = counter.Increment();
        if (current <= limit) return new Lease(counter.Decrement);
        counter.Decrement();
        throw new StorageResourceExhaustedException($"{code} limit {limit} exceeded.");
    }
    private sealed class Lease(Action release) : IDisposable
    { private Action? _release = release; public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke(); }
    private sealed class CapacityCounter
    {
        private int _value;
        public int Value => Volatile.Read(ref _value);
        public int Increment() => Interlocked.Increment(ref _value);
        public void Decrement() => Interlocked.Decrement(ref _value);
    }
}
