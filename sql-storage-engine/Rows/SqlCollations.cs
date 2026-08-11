using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace sql_storage_engine.Rows;

/// <summary>Optional host binding for byte-exact SQL Server collation weights.</summary>
public interface ISqlCollationRuntime
{
    byte[] GetSortKey(string value, bool unicode);
}

[Flags]
public enum SqlCollationComparisonFlags : ushort
{
    None = 0,
    IgnoreCase = 1,
    IgnoreNonSpace = 2,
    IgnoreKanaType = 4,
    IgnoreWidth = 8,
    Binary = 16,
    Binary2 = 32,
    SupplementaryCharacters = 64,
    VariationSelectors = 128,
    Utf8 = 256
}

/// <summary>
/// The persisted comparison and encoding metadata of a SQL Server collation.  Explicit metadata can be
/// supplied for server collations which are not installed on the current host; well-known SQL collation
/// names are resolved deterministically without consulting process culture.
/// </summary>
public sealed record SqlCollation
{
    private static readonly Regex VersionPattern = new("_(80|90|100|140|150|160)(?:_|$)", RegexOptions.CultureInvariant);
    private static readonly Regex CodePagePattern = new("(?:^|_)CP(?<code>1|437|850|125[0-8])(?:_|$)", RegexOptions.CultureInvariant);
    private static readonly ConcurrentDictionary<string, SqlCollation> Registered = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, ISqlCollationRuntime> Runtimes = new(StringComparer.OrdinalIgnoreCase);

    static SqlCollation() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public SqlCollation(string name, int lcid, byte version, SqlCollationComparisonFlags comparisonFlags,
        byte sortId, int codePage, string cultureName)
    {
        Name = SqlXmlSchemaCollection.ValidateIdentifier(name, nameof(name));
        if (lcid is < 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(lcid));
        if (codePage is < 1 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(codePage));
        _ = CultureInfo.GetCultureInfo(cultureName);
        _ = Encoding.GetEncoding(codePage);
        Lcid = checked((ushort)lcid);
        Version = version;
        ComparisonFlags = comparisonFlags;
        SortId = sortId;
        CodePage = checked((ushort)codePage);
        CultureName = cultureName;
    }

    public string Name { get; }
    public ushort Lcid { get; }
    public byte Version { get; }
    public SqlCollationComparisonFlags ComparisonFlags { get; }
    public byte SortId { get; }
    public ushort CodePage { get; }
    public string CultureName { get; }
    public bool IsUtf8 => ComparisonFlags.HasFlag(SqlCollationComparisonFlags.Utf8);
    public bool IsBinary => (ComparisonFlags & (SqlCollationComparisonFlags.Binary | SqlCollationComparisonFlags.Binary2)) != 0;
    public bool IsBinary2 => ComparisonFlags.HasFlag(SqlCollationComparisonFlags.Binary2);

    private static readonly Lazy<SqlCollation> DefaultValue = new(() => Parse("SQL_Latin1_General_CP1_CI_AS"));
    public static SqlCollation Default => DefaultValue.Value;

    public static void Register(SqlCollation collation)
    {
        ArgumentNullException.ThrowIfNull(collation);
        Registered[collation.Name] = collation;
    }

    public static void RegisterRuntime(string collationName, ISqlCollationRuntime runtime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collationName); ArgumentNullException.ThrowIfNull(runtime);
        Runtimes[collationName] = runtime;
    }

    public static SqlCollation Parse(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Collation name cannot be empty.", nameof(name));
        if (Registered.TryGetValue(name, out var registered)) return registered;
        if (name.Equals("ordinal", StringComparison.OrdinalIgnoreCase))
            return new SqlCollation("ordinal", 0x0409, 0, SqlCollationComparisonFlags.Binary2 | SqlCollationComparisonFlags.Utf8,
                0, Encoding.UTF8.CodePage, "en-US");

        var upper = name.ToUpperInvariant();
        var flags = SqlCollationComparisonFlags.None;
        if (HasToken(upper, "CI")) flags |= SqlCollationComparisonFlags.IgnoreCase;
        if (HasToken(upper, "AI")) flags |= SqlCollationComparisonFlags.IgnoreNonSpace;
        if (!HasToken(upper, "KS")) flags |= SqlCollationComparisonFlags.IgnoreKanaType;
        if (!HasToken(upper, "WS")) flags |= SqlCollationComparisonFlags.IgnoreWidth;
        if (HasToken(upper, "BIN2")) flags |= SqlCollationComparisonFlags.Binary2;
        else if (HasToken(upper, "BIN")) flags |= SqlCollationComparisonFlags.Binary;
        if (HasToken(upper, "SC")) flags |= SqlCollationComparisonFlags.SupplementaryCharacters;
        if (HasToken(upper, "VSS")) flags |= SqlCollationComparisonFlags.VariationSelectors;
        if (HasToken(upper, "UTF8")) flags |= SqlCollationComparisonFlags.Utf8;

        var (culture, lcid, codePage) = ResolveLocaleAndCodePage(upper);
        if (flags.HasFlag(SqlCollationComparisonFlags.Utf8)) codePage = Encoding.UTF8.CodePage;
        var versionMatch = VersionPattern.Match(upper);
        var version = versionMatch.Success ? versionMatch.Groups[1].Value switch
        { "90" => (byte)1, "100" => (byte)2, "140" or "150" or "160" => (byte)3, _ => (byte)0 } : (byte)0;
        var sortId = ResolveLegacySortId(upper);
        return new SqlCollation(name, lcid, version, flags, sortId, codePage, culture);
    }

    public Encoding GetEncoding() => Encoding.GetEncoding(CodePage,
        EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

    public int GetByteCount(string value) => GetEncoding().GetByteCount(value);

    public byte[] Encode(string value) => GetEncoding().GetBytes(value);

    public string Decode(ReadOnlySpan<byte> value) => GetEncoding().GetString(value);

    public byte[] GetSortKey(string value, bool unicode)
    {
        value = value.TrimEnd(' ');
        if (Runtimes.TryGetValue(Name, out var runtime)) return runtime.GetSortKey(value, unicode);
        if (IsBinary2)
            return unicode ? Encoding.BigEndianUnicode.GetBytes(value) : Encode(value);
        if (ComparisonFlags.HasFlag(SqlCollationComparisonFlags.Binary))
        {
            if (!unicode || value.Length == 0) return Encode(value);
            // Legacy BIN compares the first UTF-16 WCHAR by code point and the remaining storage bytes.
            return Encoding.BigEndianUnicode.GetBytes(value[..1])
                .Concat(Encoding.Unicode.GetBytes(value[1..])).ToArray();
        }

        var options = CompareOptions.None;
        if (ComparisonFlags.HasFlag(SqlCollationComparisonFlags.IgnoreCase)) options |= CompareOptions.IgnoreCase;
        if (ComparisonFlags.HasFlag(SqlCollationComparisonFlags.IgnoreNonSpace)) options |= CompareOptions.IgnoreNonSpace;
        if (ComparisonFlags.HasFlag(SqlCollationComparisonFlags.IgnoreKanaType)) options |= CompareOptions.IgnoreKanaType;
        if (ComparisonFlags.HasFlag(SqlCollationComparisonFlags.IgnoreWidth)) options |= CompareOptions.IgnoreWidth;
        var key = CultureInfo.GetCultureInfo(CultureName).CompareInfo.GetSortKey(value, options).KeyData;
        if (!ComparisonFlags.HasFlag(SqlCollationComparisonFlags.VariationSelectors)) return key;
        var selectors = value.EnumerateRunes().Where(rune => rune.Value is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF)
            .SelectMany(rune => BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(rune.Value))).ToArray();
        return key.Concat([byte.MaxValue]).Concat(selectors).ToArray();
    }

    public int CompareMetadataTo(SqlCollation other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var result = Lcid.CompareTo(other.Lcid);
        if (result != 0) return result;
        result = Version.CompareTo(other.Version);
        if (result != 0) return result;
        result = ((ushort)ComparisonFlags).CompareTo((ushort)other.ComparisonFlags);
        return result != 0 ? result : SortId.CompareTo(other.SortId);
    }

    private static bool HasToken(string value, string token) =>
        value.Split('_', StringSplitOptions.RemoveEmptyEntries).Contains(token, StringComparer.Ordinal);

    private static (string Culture, int Lcid, int CodePage) ResolveLocaleAndCodePage(string name)
    {
        var explicitCodePage = CodePagePattern.Match(name);
        var requestedCodePage = explicitCodePage.Success
            ? explicitCodePage.Groups["code"].Value == "1" ? 1252 : int.Parse(explicitCodePage.Groups["code"].Value, CultureInfo.InvariantCulture)
            : (int?)null;
        if (name.Contains("JAPANESE", StringComparison.Ordinal)) return ("ja-JP", 0x0411, 932);
        if (name.Contains("KOREAN", StringComparison.Ordinal)) return ("ko-KR", 0x0412, 949);
        if (name.Contains("CHINESE_PRC", StringComparison.Ordinal) || name.Contains("CHINESE_SIMPLIFIED", StringComparison.Ordinal))
            return ("zh-CN", 0x0804, 936);
        if (name.Contains("CHINESE", StringComparison.Ordinal)) return ("zh-TW", 0x0404, 950);
        if (name.Contains("THAI", StringComparison.Ordinal)) return ("th-TH", 0x041e, 874);
        if (name.Contains("VIETNAMESE", StringComparison.Ordinal)) return ("vi-VN", 0x042a, 1258);
        if (name.Contains("UKRAINIAN", StringComparison.Ordinal)) return ("uk-UA", 0x0422, 1251);
        if (name.Contains("MACEDONIAN", StringComparison.Ordinal)) return ("mk-MK", 0x042f, 1251);
        if (name.Contains("CYRILLIC", StringComparison.Ordinal) || name.Contains("SERBIAN_CYRILLIC", StringComparison.Ordinal)) return ("ru-RU", 0x0419, 1251);
        if (name.Contains("GREEK", StringComparison.Ordinal)) return ("el-GR", 0x0408, 1253);
        if (name.Contains("TURKISH", StringComparison.Ordinal)) return ("tr-TR", 0x041f, 1254);
        if (name.Contains("HEBREW", StringComparison.Ordinal)) return ("he-IL", 0x040d, 1255);
        if (name.Contains("ARABIC", StringComparison.Ordinal)) return ("ar-SA", 0x0401, 1256);
        if (name.Contains("ESTONIAN", StringComparison.Ordinal)) return ("et-EE", 0x0425, 1257);
        if (name.Contains("LATVIAN", StringComparison.Ordinal)) return ("lv-LV", 0x0426, 1257);
        if (name.Contains("BALTIC", StringComparison.Ordinal) || name.Contains("LITHUANIAN", StringComparison.Ordinal)) return ("lt-LT", 0x0427, 1257);
        if (name.Contains("POLISH", StringComparison.Ordinal)) return ("pl-PL", 0x0415, 1250);
        if (name.Contains("CZECH", StringComparison.Ordinal)) return ("cs-CZ", 0x0405, 1250);
        if (name.Contains("HUNGARIAN", StringComparison.Ordinal)) return ("hu-HU", 0x040e, 1250);
        if (name.Contains("ROMANIAN", StringComparison.Ordinal)) return ("ro-RO", 0x0418, 1250);
        if (name.Contains("CROATIAN", StringComparison.Ordinal)) return ("hr-HR", 0x041a, 1250);
        if (name.Contains("SLOVAK", StringComparison.Ordinal)) return ("sk-SK", 0x041b, 1250);
        if (name.Contains("SLOVENIAN", StringComparison.Ordinal)) return ("sl-SI", 0x0424, 1250);
        if (name.Contains("FRENCH", StringComparison.Ordinal)) return ("fr-FR", 0x040c, requestedCodePage ?? 1252);
        if (name.Contains("GERMAN", StringComparison.Ordinal)) return ("de-DE", 0x0407, requestedCodePage ?? 1252);
        if (name.Contains("SPANISH", StringComparison.Ordinal)) return ("es-ES", 0x0c0a, requestedCodePage ?? 1252);
        if (name.Contains("ITALIAN", StringComparison.Ordinal)) return ("it-IT", 0x0410, requestedCodePage ?? 1252);
        if (name.Contains("DUTCH", StringComparison.Ordinal)) return ("nl-NL", 0x0413, requestedCodePage ?? 1252);
        if (name.Contains("FINNISH", StringComparison.Ordinal) || name.Contains("SWEDISH", StringComparison.Ordinal)) return ("sv-SE", 0x041d, requestedCodePage ?? 1252);
        if (name.Contains("DANISH", StringComparison.Ordinal) || name.Contains("NORWEGIAN", StringComparison.Ordinal)) return ("da-DK", 0x0406, requestedCodePage ?? 1252);
        if (name.Contains("ICELANDIC", StringComparison.Ordinal)) return ("is-IS", 0x040f, requestedCodePage ?? 1252);
        if (name.Contains("LATIN1_GENERAL", StringComparison.Ordinal)) return ("en-US", 0x0409, requestedCodePage ?? 1252);
        throw new ArgumentException($"Unknown SQL Server collation '{name}'. Register explicit metadata with {nameof(Register)}.", nameof(name));
    }

    private static byte ResolveLegacySortId(string name)
    {
        if (!name.StartsWith("SQL_", StringComparison.Ordinal)) return 0;
        if (name.Contains("CP1", StringComparison.Ordinal)) return 52;
        return 0;
    }
}
