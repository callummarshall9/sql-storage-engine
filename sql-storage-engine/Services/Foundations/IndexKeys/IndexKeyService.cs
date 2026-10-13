// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Foundations.IndexKeys;
internal sealed partial class IndexKeyService : IIndexKeyService
{
    public sql_storage_engine.Models.Indexes.IndexKey CreateIndexKey(ReadOnlySpan<byte> bytes) =>
        TryCatch(operation: static context =>
    {
        ValidateIndexKeySource(bytes: context.bytes);

        return new sql_storage_engine.Models.Indexes.IndexKey
        {
            Content = string.Create(length: context.bytes.Length, state: context.bytes, action: static (destination, source) =>
            {
                for (var index = 0; index < source.Length; index++)
                {
                    destination[index] = (char)source[index];
                }
            })
        };
    }, context: new ReadableIndexKeyContext { bytes = bytes });

    public ReadOnlyMemory<byte> ReadIndexKeyBytes(sql_storage_engine.Models.Indexes.IndexKey indexKey) =>
        TryCatch(operation: () =>
    {
        ValidateIndexKey(indexKey: indexKey);
        return new ReadOnlyMemory<byte>(ReadIndexKeyBytesCore(indexKey: indexKey));
    });

    public int CompareIndexKeys(sql_storage_engine.Models.Indexes.IndexKey? [] indexKeys) =>
        TryCatch(operation: () =>
    {
        ValidateIndexKeyComparison(indexKeys: indexKeys);
        return indexKeys[1] is null ? 1 : string.Compare(strA: indexKeys[0]!.Content, strB: indexKeys[1]!.Content, comparisonType: StringComparison.Ordinal);
    });

    public string FormatIndexKey(sql_storage_engine.Models.Indexes.IndexKey indexKey) =>
        TryCatch(operation: () =>
    {
        ValidateIndexKey(indexKey: indexKey);
        return Convert.ToHexString(inArray: ReadIndexKeyBytesCore(indexKey: indexKey));
    });

    public void CheckIndexKey(sql_storage_engine.Models.Indexes.IndexKey indexKey) =>
        TryCatch(operation: () =>
    {
        ValidateIndexKey(indexKey: indexKey);
    });

    private static byte[] ReadIndexKeyBytesCore(IndexKey indexKey)
    {
        var bytes = new byte[indexKey.Content.Length];

        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)indexKey.Content[index];
        }

        return bytes;
    }
}