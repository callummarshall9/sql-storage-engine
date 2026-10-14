// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Foundations.IndexKeys;
internal interface IIndexKeyService
{
    sql_storage_engine.Models.Indexes.IndexKey CreateIndexKey(ReadOnlySpan<byte> bytes);

    ReadOnlyMemory<byte> ReadIndexKeyBytes(sql_storage_engine.Models.Indexes.IndexKey indexKey);

    int CompareIndexKeys(sql_storage_engine.Models.Indexes.IndexKey? [] indexKeys);

    string FormatIndexKey(sql_storage_engine.Models.Indexes.IndexKey indexKey);

    void CheckIndexKey(sql_storage_engine.Models.Indexes.IndexKey indexKey);
}