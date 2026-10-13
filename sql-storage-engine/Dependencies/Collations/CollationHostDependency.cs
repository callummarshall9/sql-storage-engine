// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace sql_storage_engine.Dependencies.Collations;
internal sealed class CollationHostDependency : Encoding
{
    public CollationHostDependency() : base(65001, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
    {
    }

    private static readonly Encoding utf8 = new UTF8Encoding(false, true);
    private static readonly ConcurrentDictionary<string, Func<string, bool, byte[]>> runtimes = new(StringComparer.OrdinalIgnoreCase);
    static CollationHostDependency()
    {
        Encoding.RegisterProvider(provider: CodePagesEncodingProvider.Instance);
    }

    public override int GetByteCount(char[] chars, int index, int count) =>
        utf8.GetByteCount(chars: chars, index: index, count: count);

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) =>
        utf8.GetBytes(chars: chars, charIndex: charIndex, charCount: charCount, bytes: bytes, byteIndex: byteIndex);

    public override int GetCharCount(byte[] bytes, int index, int count) =>
        utf8.GetCharCount(bytes: bytes, index: index, count: count);

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) =>
        utf8.GetChars(bytes: bytes, byteIndex: byteIndex, byteCount: byteCount, chars: chars, charIndex: charIndex);

    public override int GetMaxByteCount(int charCount) =>
        utf8.GetMaxByteCount(charCount: charCount);

    public override int GetMaxCharCount(int byteCount) =>
        utf8.GetMaxCharCount(byteCount: byteCount);

    public void CheckHostCollation(string cultureName, int codePage)
    {
        _ = CultureInfo.GetCultureInfo(name: cultureName);
        _ = Encoding.GetEncoding(codepage: codePage);
    }

    public Encoding ReadCodePageEncoding(int codePage) =>
        codePage == 65001 ? this : Encoding.GetEncoding(codepage: codePage, encoderFallback: EncoderFallback.ExceptionFallback, decoderFallback: DecoderFallback.ExceptionFallback);

    public int CountCodePageBytes(int codePage, string value) =>
        ReadCodePageEncoding(codePage: codePage)
            .GetByteCount(s: value);

    public byte[] EncodeCodePage(int codePage, string value) =>
        ReadCodePageEncoding(codePage: codePage)
            .GetBytes(s: value);

    public string DecodeCodePage(int codePage, ReadOnlySpan<byte> value) =>
        ReadCodePageEncoding(codePage: codePage)
            .GetString(bytes: value);

    public byte[] EncodeUnicode(string value, bool bigEndian) =>
        (bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode).GetBytes(s: value);

    public byte[] CalculateCultureSortKey(string cultureName, string value, int options) =>
        CultureInfo.GetCultureInfo(name: cultureName)
            .CompareInfo.GetSortKey(source: value, options: (CompareOptions)options)
            .KeyData;

    public void RegisterHostCollationRuntime(string collationName, Func<string, bool, byte[]> runtime)
    {
        runtimes[collationName] = runtime;
    }

    public byte[]? ReadHostCollationRuntimeSortKey(string collationName, string value, bool unicode) =>
        runtimes.TryGetValue(key: collationName, value: out var runtime) ? runtime(arg1: value, arg2: unicode) : null;

    public void WriteCodePage(int codePage, string value, Span<byte> destination) =>
        ReadCodePageEncoding(codePage: codePage)
            .GetBytes(chars: value, bytes: destination);
}