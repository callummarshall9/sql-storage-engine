using SqlExecutionEngine.Storage.Abstractions;

namespace SqlExecutionEngine.Storage.Conformance;

internal static class BackendOracle
{
    internal static readonly CollectionId Collection = new(new Guid("f175d411-c688-4d9e-899e-341ef8caa001"));
    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
    internal static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new InvalidDataException($"Expected {typeof(T).Name}.");
    }
    internal static async Task RowsAsync(IBackendSnapshot snapshot, KeyRange range,
        (byte[] Key, byte[] Value)[] expected, CancellationToken token)
    {
        var index = 0;
        await foreach (var row in snapshot.ScanAsync(Collection, range, token).WithCancellation(token))
        {
            Require(index < expected.Length, "Unexpected scan row.");
            Require(row.Key.ToArray().SequenceEqual(expected[index].Key), "Scan key/order differs from independent vector.");
            Require(row.Value.ToArray().SequenceEqual(expected[index].Value), "Scan value differs from independent vector.");
            index++;
        }
        Require(index == expected.Length, "Missing scan row.");
    }
    internal static BackendBatch Batch(IBackendStore store, IBackendSnapshot snapshot, params BackendMutation[] mutations)
        => new(new(Guid.NewGuid()), store.Id, snapshot.Generation, mutations);
    internal static void Receipt(CommitReceipt receipt, BackendBatch batch, CommitStatus status)
    {
        Require(receipt.Store == batch.Store && receipt.Operation == batch.Operation && receipt.Status == status,
            "Receipt identity or outcome differs from submitted operation.");
    }
}
