// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Services.Foundations.IndexKeys;

namespace sql_storage_engine.Exposures.Indexes;
public static class IndexKeyExposure
{
    private static readonly IIndexKeyService service = new IndexKeyService();
    public static sql_storage_engine.Models.Indexes.IndexKey CreateIndexKey(ReadOnlySpan<byte> bytes) =>
        service.CreateIndexKey(bytes: bytes);

    public static ReadOnlyMemory<byte> ReadIndexKeyBytes(sql_storage_engine.Models.Indexes.IndexKey indexKey) =>
        service.ReadIndexKeyBytes(indexKey: indexKey);

    public static int CompareIndexKeys(sql_storage_engine.Models.Indexes.IndexKey? [] indexKeys) =>
        service.CompareIndexKeys(indexKeys: indexKeys);

    public static string FormatIndexKey(sql_storage_engine.Models.Indexes.IndexKey indexKey) =>
        service.FormatIndexKey(indexKey: indexKey);

    public static void CheckIndexKey(sql_storage_engine.Models.Indexes.IndexKey indexKey) =>
        service.CheckIndexKey(indexKey: indexKey);

}