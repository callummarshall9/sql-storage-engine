// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Text;

namespace sql_storage_engine.Brokers.Collations;
internal interface ICollationHostBroker
{
    void CheckHostCollation(string cultureName, int codePage);

    Encoding ReadCodePageEncoding(int codePage);

    int CountCodePageBytes(int codePage, string value);

    byte[] EncodeCodePage(int codePage, string value);

    string DecodeCodePage(int codePage, ReadOnlySpan<byte> value);

    byte[] EncodeUnicode(string value, bool bigEndian);

    byte[] CalculateCultureSortKey(string cultureName, string value, int options);

    void RegisterHostCollationRuntime(string collationName, Func<string, bool, byte[]> runtime);

    byte[]? ReadHostCollationRuntimeSortKey(string collationName, string value, bool unicode);

    void WriteCodePage(int codePage, string value, Span<byte> destination);
}