using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

public interface IWalDevice
{
    long Length { get; }
    ValueTask<int> ReadAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken = default);
    ValueTask<int> WriteAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default);
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
    ValueTask RollSegmentAsync(ulong segmentNumber, CancellationToken cancellationToken = default);
    void Truncate(long length);
}
