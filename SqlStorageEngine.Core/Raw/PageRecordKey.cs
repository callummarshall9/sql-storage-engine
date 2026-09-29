using SqlExecutionEngine.Storage.Abstractions;

namespace SqlStorageEngine.Core;

internal readonly record struct PageRecordKey(CollectionId Collection, ByteString Key);
