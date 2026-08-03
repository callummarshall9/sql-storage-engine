using System.Text.Json;
using sql_storage_engine.Backup;
using sql_storage_engine.Catalog;
using sql_storage_engine.Heap;
using sql_storage_engine.Indexes;
using sql_storage_engine.Logging;
using sql_storage_engine.Overflow;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Diagnostics;

public sealed record FuzzTarget(string Name, int MaximumInputBytes, Action<ReadOnlyMemory<byte>> Decode,
    IReadOnlyList<ReadOnlyMemory<byte>> GoldenSeeds);
public sealed record FuzzFailure(string Target, byte[] Input, string FailureType, string Message);
public interface IFuzzRegressionSink { void Save(FuzzFailure failure); }

/// <summary>Runs fixed-seed mutations through bounded decoder entry points and persists every unsafe failure.</summary>
public sealed class PersistentFormatFuzzHarness(IFuzzRegressionSink regressionSink, TimeSpan? timeout = null)
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(1);

    public async Task<IReadOnlyList<FuzzFailure>> RunAsync(FuzzTarget target, int iterations, int seed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);
        if (target.MaximumInputBytes <= 0 || target.GoldenSeeds.Count == 0) throw new ArgumentException("Fuzz target bounds and seeds are required.", nameof(target));
        var random = new Random(seed);
        var failures = new List<FuzzFailure>();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = target.GoldenSeeds[iteration % target.GoldenSeeds.Count].ToArray();
            var length = random.Next(0, Math.Min(target.MaximumInputBytes, Math.Max(1, source.Length * 2)) + 1);
            var input = new byte[length];
            source.AsSpan(0, Math.Min(source.Length, input.Length)).CopyTo(input);
            for (var mutation = 0; mutation < Math.Min(8, input.Length); mutation++) input[random.Next(input.Length)] = (byte)random.Next(256);
            var task = Task.Run(() => target.Decode(input), cancellationToken);
            var completed = await Task.WhenAny(task, Task.Delay(_timeout, cancellationToken)).ConfigureAwait(false);
            FuzzFailure? failure = null;
            if (completed != task) failure = new(target.Name, input, "TIMEOUT", "Decoder exceeded its execution bound.");
            else
            {
                try { await task.ConfigureAwait(false); }
                catch (Exception exception) when (exception is ArgumentException or StorageException or JsonException or OverflowException) { }
                catch (Exception exception) { failure = new(target.Name, input, exception.GetType().Name, exception.Message); }
            }
            if (failure is not null) { regressionSink.Save(failure); failures.Add(failure); }
        }
        return failures.AsReadOnly();
    }

    public static IReadOnlyList<FuzzTarget> CreateCoreTargets()
    {
        var page = new byte[PageConstants.DefaultSize];
        PageHeaderCodec.Write(page, new PageHeader(new(1), PageType.Heap, PageFormatVersion.Current, default, PageChecksumAlgorithm.Crc32, 0));
        PageChecksum.WriteChecksum(page, page.Length);
        var wal = WalFormat.WriteRecord(new WalRecord(new(1), default, new(1), WalRecordType.Commit, ReadOnlyMemory<byte>.Empty));
        return new FuzzTarget[]
        {
            Target("database-header", bytes => DatabaseHeaderCodec.Read(bytes.Span), page),
            Target("page-header", bytes => PageHeaderCodec.Read(bytes.Span), page),
            Target("heap-slots", bytes => HeapPageLayout.ReadHeader(bytes.Span), page),
            Target("rows", bytes => CatalogCodec.Decode(bytes.Span), [0,0,0,0]),
            Target("keys", bytes => _ = new IndexKey(bytes.Span), [1]),
            Target("catalog-records", bytes => CatalogCodec.Decode(bytes.Span), [0,0,0,0]),
            Target("overflow-pages", bytes => OverflowPageCodec.ReadHeader(bytes.Span, new(1)), page),
            Target("index-pages", bytes => LeafIndexPageCodec.Read(bytes.Span, new(1)), page),
            Target("wal", bytes => WalFormat.ReadRecords(bytes.Span), wal),
            Target("backup-manifests", bytes => JsonSerializer.Deserialize<BackupManifest>(bytes.Span), "{}"u8.ToArray())
        };
    }

    private static FuzzTarget Target(string name, Action<ReadOnlyMemory<byte>> decode, byte[] seed) =>
        new(name, 16 * 1024 * 1024, decode, new ReadOnlyMemory<byte>[] { seed });
}
