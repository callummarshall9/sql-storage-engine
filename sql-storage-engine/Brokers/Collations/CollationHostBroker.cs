// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Text;
using sql_storage_engine.Dependencies.Collations;

namespace sql_storage_engine.Brokers.Collations;
internal sealed class CollationHostBroker : ICollationHostBroker
{
    private static readonly CollationHostDependency dependency = new();
    public void CheckHostCollation(string cultureName, int codePage) =>
        dependency.CheckHostCollation(cultureName: cultureName, codePage: codePage);

    public Encoding ReadCodePageEncoding(int codePage) =>
        dependency.ReadCodePageEncoding(codePage: codePage);

    public int CountCodePageBytes(int codePage, string value) =>
        dependency.CountCodePageBytes(codePage: codePage, value: value);

    public byte[] EncodeCodePage(int codePage, string value) =>
        dependency.EncodeCodePage(codePage: codePage, value: value);

    public string DecodeCodePage(int codePage, ReadOnlySpan<byte> value) =>
        dependency.DecodeCodePage(codePage: codePage, value: value);

    public byte[] EncodeUnicode(string value, bool bigEndian) =>
        dependency.EncodeUnicode(value: value, bigEndian: bigEndian);

    public byte[] CalculateCultureSortKey(string cultureName, string value, int options) =>
        dependency.CalculateCultureSortKey(cultureName: cultureName, value: value, options: options);

    public void RegisterHostCollationRuntime(string collationName, Func<string, bool, byte[]> runtime) =>
        dependency.RegisterHostCollationRuntime(collationName: collationName, runtime: runtime);

    public byte[]? ReadHostCollationRuntimeSortKey(string collationName, string value, bool unicode) =>
        dependency.ReadHostCollationRuntimeSortKey(collationName: collationName, value: value, unicode: unicode);

    public void WriteCodePage(int codePage, string value, Span<byte> destination) =>
        dependency.WriteCodePage(codePage: codePage, value: value, destination: destination);
}