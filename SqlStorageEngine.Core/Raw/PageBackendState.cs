using SqlExecutionEngine.Storage.Abstractions;

namespace SqlStorageEngine.Core;

internal sealed class PageBackendState(StoreId id)
{
    internal StoreId Id { get; } = id;
    internal Dictionary<PageRecordKey, ByteString> Records { get; } = [];
    internal Dictionary<OperationId, PageReceipt> Receipts { get; } = [];
    internal long RecordBytes => Records.Sum(p => 25L + p.Key.Key.Length + p.Value.Length);

    internal PageBackendState Copy()
    {
        var result = new PageBackendState(Id);
        foreach (var pair in Records) result.Records.Add(pair.Key, pair.Value);
        foreach (var pair in Receipts) result.Receipts.Add(pair.Key, pair.Value);
        return result;
    }
}
