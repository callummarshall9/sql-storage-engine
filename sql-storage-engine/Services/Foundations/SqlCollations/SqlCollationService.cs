// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Text;
using System.Collections.Concurrent;
using System.Globalization;
using System.Buffers.Binary;
using System.Linq;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Brokers.Collations;

namespace sql_storage_engine.Services.Foundations.SqlCollations;
internal sealed partial class SqlCollationService : ISqlCollationService
{
    private static readonly ICollationHostBroker host = new CollationHostBroker();
    private static readonly SqlCollationRegistry registry = new()
    {
        Registered = new ConcurrentDictionary<string, SqlCollation>(StringComparer.OrdinalIgnoreCase)
    };
    public SqlCollation CreateSqlCollation(string name, int lcid, byte version, SqlCollationComparisonFlags comparisonFlags, byte sortId, int codePage, string cultureName) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationCreation(name: name, lcid: lcid, version: version, comparisonFlags: comparisonFlags, sortId: sortId, codePage: codePage, cultureName: cultureName);
        return CreateSqlCollationCore(name: name, lcid: lcid, version: version, comparisonFlags: comparisonFlags, sortId: sortId, codePage: codePage, cultureName: cultureName);
    });

    public SqlCollation ParseSqlCollation(string name) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationName(name: name);
        return ParseCore(name: name);
    });

    public SqlCollation ReadDefaultSqlCollation() =>
        TryCatch(operation: () =>
    {
        return ParseCore(name: "SQL_Latin1_General_CP1_CI_AS");
    });

    public void RegisterSqlCollation(SqlCollation sqlCollation) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationMetadata(sqlCollation: sqlCollation);
        registry.Registered[sqlCollation.Name] = sqlCollation;
    });

    public void RegisterSqlCollationRuntime(string collationName, Func<string, bool, byte[]> runtime) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationRuntime(collationName: collationName, runtime: runtime);
        host.RegisterHostCollationRuntime(collationName: collationName, runtime: runtime);
    });

    public Encoding ReadSqlCollationEncoding(SqlCollation sqlCollation) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationMetadata(sqlCollation: sqlCollation);
        return host.ReadCodePageEncoding(codePage: sqlCollation.CodePage);
    });

    public int CountSqlCollationBytes(SqlCollation sqlCollation, string value) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationText(sqlCollation: sqlCollation, value: value);
        return host.CountCodePageBytes(codePage: sqlCollation.CodePage, value: value);
    });

    public byte[] EncodeSqlCollation(SqlCollation sqlCollation, string value) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationText(sqlCollation: sqlCollation, value: value);
        return host.EncodeCodePage(codePage: sqlCollation.CodePage, value: value);
    });

    public string DecodeSqlCollation(SqlCollation sqlCollation, ReadOnlySpan<byte> value) =>
        TryCatch(operation: static context =>
    {
        ValidateSqlCollationBytes(sqlCollation: context.sqlCollation, value: context.value);
        return host.DecodeCodePage(codePage: context.sqlCollation.CodePage, value: context.value);
    }, context: new ReadableSqlCollationContext { sqlCollation = sqlCollation, value = value });

    public byte[] CalculateSqlCollationSortKey(SqlCollation sqlCollation, string value, bool unicode) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationSortKey(sqlCollation: sqlCollation, value: value, unicode: unicode);
        return SortCore(sqlCollation: sqlCollation, value: value, unicode: unicode);
    });

    public int CompareSqlCollationMetadata(SqlCollation[] sqlCollations) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationComparison(sqlCollations: sqlCollations);
        var left = sqlCollations[0];
        var right = sqlCollations[1];
        var result = left.Lcid.CompareTo(value: right.Lcid);

        if (result != 0)
        {
            return result;
        }

        result = left.Version.CompareTo(value: right.Version);

        if (result != 0)
        {
            return result;
        }

        result = ((ushort)left.ComparisonFlags).CompareTo(value: (ushort)right.ComparisonFlags);
        return result != 0 ? result : left.SortId.CompareTo(value: right.SortId);
    });

    public bool CheckSqlCollationIsUtf8(SqlCollation sqlCollation) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationMetadata(sqlCollation: sqlCollation);
        return (sqlCollation.ComparisonFlags & (SqlCollationComparisonFlags.Utf8)) != 0;
    });

    public bool CheckSqlCollationIsBinary(SqlCollation sqlCollation) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationMetadata(sqlCollation: sqlCollation);
        return (sqlCollation.ComparisonFlags & (SqlCollationComparisonFlags.Binary | SqlCollationComparisonFlags.Binary2)) != 0;
    });

    public bool CheckSqlCollationIsBinary2(SqlCollation sqlCollation) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationMetadata(sqlCollation: sqlCollation);
        return (sqlCollation.ComparisonFlags & (SqlCollationComparisonFlags.Binary2)) != 0;
    });

    private static SqlCollation CreateSqlCollationCore(string name, int lcid, byte version, SqlCollationComparisonFlags comparisonFlags, byte sortId, int codePage, string cultureName)
    {
        ValidateSqlCollationCreation(name: name, lcid: lcid, version: version, comparisonFlags: comparisonFlags, sortId: sortId, codePage: codePage, cultureName: cultureName);

        return new SqlCollation
        {
            Name = name,
            Lcid = checked((ushort)lcid),
            Version = version,
            ComparisonFlags = comparisonFlags,
            SortId = sortId,
            CodePage = checked((ushort)codePage),
            CultureName = cultureName
        };
    }

    private static SqlCollation ParseCore(string name)
    {
        if (string.IsNullOrWhiteSpace(value: name))
        {
            throw new ArgumentException("Collation name cannot be empty.", nameof(name));
        }

        if (registry.Registered.TryGetValue(key: name, value: out var registered))
        {
            return registered;
        }

        if (name.Equals(value: "ordinal", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return CreateSqlCollationCore(name: "ordinal", lcid: 0x0409, version: 0, comparisonFlags: SqlCollationComparisonFlags.Binary2 | SqlCollationComparisonFlags.Utf8, sortId: 0, codePage: 65001, cultureName: "en-US");
        }

        var upper = name.ToUpperInvariant();
        var flags = SqlCollationComparisonFlags.None;

        if (HasToken(value: upper, token: "CI"))
        {
            flags |= SqlCollationComparisonFlags.IgnoreCase;
        }

        if (HasToken(value: upper, token: "AI"))
        {
            flags |= SqlCollationComparisonFlags.IgnoreNonSpace;
        }

        if (!HasToken(value: upper, token: "KS"))
        {
            flags |= SqlCollationComparisonFlags.IgnoreKanaType;
        }

        if (!HasToken(value: upper, token: "WS"))
        {
            flags |= SqlCollationComparisonFlags.IgnoreWidth;
        }

        if (HasToken(value: upper, token: "BIN2"))
        {
            flags |= SqlCollationComparisonFlags.Binary2;
        }
        else if (HasToken(value: upper, token: "BIN"))
        {
            flags |= SqlCollationComparisonFlags.Binary;
        }

        if (HasToken(value: upper, token: "SC"))
        {
            flags |= SqlCollationComparisonFlags.SupplementaryCharacters;
        }

        if (HasToken(value: upper, token: "VSS"))
        {
            flags |= SqlCollationComparisonFlags.VariationSelectors;
        }

        if (HasToken(value: upper, token: "UTF8"))
        {
            flags |= SqlCollationComparisonFlags.Utf8;
        }

        var(culture, lcid, codePage) = ResolveLocaleAndCodePage(name: upper);

        if (flags.HasFlag(flag: SqlCollationComparisonFlags.Utf8))
        {
            codePage = 65001;
        }

        var versionToken = upper.Split(separator: '_')
            .Skip(count: 1)
            .FirstOrDefault(predicate: token => token is "80" or "90" or "100" or "140" or "150" or "160");

        var version = versionToken is not null ? versionToken switch
        {
            "90" => (byte)1,
            "100" => (byte)2,
            "140" or "150" or "160" => (byte)3,
            _ => (byte)0
        } : (byte)0;

        var sortId = ResolveLegacySortId(name: upper);
        return CreateSqlCollationCore(name: name, lcid: lcid, version: version, comparisonFlags: flags, sortId: sortId, codePage: codePage, cultureName: culture);
    }

    private static byte[] SortCore(SqlCollation sqlCollation, string value, bool unicode)
    {
        value = value.TrimEnd(trimChar: ' ');
        var runtimeKey = host.ReadHostCollationRuntimeSortKey(collationName: sqlCollation.Name, value: value, unicode: unicode);

        if (runtimeKey is not null)
        {
            return runtimeKey;
        }

        if ((sqlCollation.ComparisonFlags & SqlCollationComparisonFlags.Binary2) != 0)
        {
            return unicode ? host.EncodeUnicode(value: value, bigEndian: true) : host.EncodeCodePage(codePage: sqlCollation.CodePage, value: value);
        }

        if (sqlCollation.ComparisonFlags.HasFlag(flag: SqlCollationComparisonFlags.Binary))
        {
            if (!unicode || value.Length == 0)
            {
                return host.EncodeCodePage(codePage: sqlCollation.CodePage, value: value);
            }

            return host.EncodeUnicode(value: value[..1], bigEndian: true)
            .Concat(second: host.EncodeUnicode(value: value[1..], bigEndian: false))
            .ToArray();
        }

        var options = CompareOptions.None;

        if (sqlCollation.ComparisonFlags.HasFlag(flag: SqlCollationComparisonFlags.IgnoreCase))
        {
            options |= CompareOptions.IgnoreCase;
        }

        if (sqlCollation.ComparisonFlags.HasFlag(flag: SqlCollationComparisonFlags.IgnoreNonSpace))
        {
            options |= CompareOptions.IgnoreNonSpace;
        }

        if (sqlCollation.ComparisonFlags.HasFlag(flag: SqlCollationComparisonFlags.IgnoreKanaType))
        {
            options |= CompareOptions.IgnoreKanaType;
        }

        if (sqlCollation.ComparisonFlags.HasFlag(flag: SqlCollationComparisonFlags.IgnoreWidth))
        {
            options |= CompareOptions.IgnoreWidth;
        }

        var key = host.CalculateCultureSortKey(cultureName: sqlCollation.CultureName, value: value, options: (int)options);

        if (!sqlCollation.ComparisonFlags.HasFlag(flag: SqlCollationComparisonFlags.VariationSelectors))
        {
            return key;
        }

        var selectors = value.EnumerateRunes()
            .Where(predicate: rune => rune.Value is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF)
            .SelectMany(selector: rune => BitConverter.GetBytes(value: BinaryPrimitives.ReverseEndianness(value: rune.Value)))
            .ToArray();

        return key.Concat(second: [byte.MaxValue])
            .Concat(second: selectors)
            .ToArray();
    }

    private static (string Culture, int Lcid, int CodePage) ResolveLocaleAndCodePage(string name)
    {
        var explicitCodePage = name.Split(separator: '_')
            .FirstOrDefault(predicate: token => token is "CP1" or "CP437" or "CP850" or "CP1250" or "CP1251" or "CP1252" or "CP1253" or "CP1254" or "CP1255" or "CP1256" or "CP1257" or "CP1258");

        var requestedCodePage = explicitCodePage is not null ? explicitCodePage == "CP1" ? 1252 : int.Parse(s: explicitCodePage[2..], provider: CultureInfo.InvariantCulture) : (int? )null;

        if (name.Contains(value: "JAPANESE", comparisonType: StringComparison.Ordinal))
        {
            return ("ja-JP", 0x0411, 932);
        }

        if (name.Contains(value: "KOREAN", comparisonType: StringComparison.Ordinal))
        {
            return ("ko-KR", 0x0412, 949);
        }

        if (name.Contains(value: "CHINESE_PRC", comparisonType: StringComparison.Ordinal) || name.Contains(value: "CHINESE_SIMPLIFIED", comparisonType: StringComparison.Ordinal))
        {
            return ("zh-CN", 0x0804, 936);
        }

        if (name.Contains(value: "CHINESE", comparisonType: StringComparison.Ordinal))
        {
            return ("zh-TW", 0x0404, 950);
        }

        if (name.Contains(value: "THAI", comparisonType: StringComparison.Ordinal))
        {
            return ("th-TH", 0x041e, 874);
        }

        if (name.Contains(value: "VIETNAMESE", comparisonType: StringComparison.Ordinal))
        {
            return ("vi-VN", 0x042a, 1258);
        }

        if (name.Contains(value: "UKRAINIAN", comparisonType: StringComparison.Ordinal))
        {
            return ("uk-UA", 0x0422, 1251);
        }

        if (name.Contains(value: "MACEDONIAN", comparisonType: StringComparison.Ordinal))
        {
            return ("mk-MK", 0x042f, 1251);
        }

        if (name.Contains(value: "CYRILLIC", comparisonType: StringComparison.Ordinal) || name.Contains(value: "SERBIAN_CYRILLIC", comparisonType: StringComparison.Ordinal))
        {
            return ("ru-RU", 0x0419, 1251);
        }

        if (name.Contains(value: "GREEK", comparisonType: StringComparison.Ordinal))
        {
            return ("el-GR", 0x0408, 1253);
        }

        if (name.Contains(value: "TURKISH", comparisonType: StringComparison.Ordinal))
        {
            return ("tr-TR", 0x041f, 1254);
        }

        if (name.Contains(value: "HEBREW", comparisonType: StringComparison.Ordinal))
        {
            return ("he-IL", 0x040d, 1255);
        }

        if (name.Contains(value: "ARABIC", comparisonType: StringComparison.Ordinal))
        {
            return ("ar-SA", 0x0401, 1256);
        }

        if (name.Contains(value: "ESTONIAN", comparisonType: StringComparison.Ordinal))
        {
            return ("et-EE", 0x0425, 1257);
        }

        if (name.Contains(value: "LATVIAN", comparisonType: StringComparison.Ordinal))
        {
            return ("lv-LV", 0x0426, 1257);
        }

        if (name.Contains(value: "BALTIC", comparisonType: StringComparison.Ordinal) || name.Contains(value: "LITHUANIAN", comparisonType: StringComparison.Ordinal))
        {
            return ("lt-LT", 0x0427, 1257);
        }

        if (name.Contains(value: "POLISH", comparisonType: StringComparison.Ordinal))
        {
            return ("pl-PL", 0x0415, 1250);
        }

        if (name.Contains(value: "CZECH", comparisonType: StringComparison.Ordinal))
        {
            return ("cs-CZ", 0x0405, 1250);
        }

        if (name.Contains(value: "HUNGARIAN", comparisonType: StringComparison.Ordinal))
        {
            return ("hu-HU", 0x040e, 1250);
        }

        if (name.Contains(value: "ROMANIAN", comparisonType: StringComparison.Ordinal))
        {
            return ("ro-RO", 0x0418, 1250);
        }

        if (name.Contains(value: "CROATIAN", comparisonType: StringComparison.Ordinal))
        {
            return ("hr-HR", 0x041a, 1250);
        }

        if (name.Contains(value: "SLOVAK", comparisonType: StringComparison.Ordinal))
        {
            return ("sk-SK", 0x041b, 1250);
        }

        if (name.Contains(value: "SLOVENIAN", comparisonType: StringComparison.Ordinal))
        {
            return ("sl-SI", 0x0424, 1250);
        }

        if (name.Contains(value: "FRENCH", comparisonType: StringComparison.Ordinal))
        {
            return ("fr-FR", 0x040c, requestedCodePage ?? 1252);
        }

        if (name.Contains(value: "GERMAN", comparisonType: StringComparison.Ordinal))
        {
            return ("de-DE", 0x0407, requestedCodePage ?? 1252);
        }

        if (name.Contains(value: "SPANISH", comparisonType: StringComparison.Ordinal))
        {
            return ("es-ES", 0x0c0a, requestedCodePage ?? 1252);
        }

        if (name.Contains(value: "ITALIAN", comparisonType: StringComparison.Ordinal))
        {
            return ("it-IT", 0x0410, requestedCodePage ?? 1252);
        }

        if (name.Contains(value: "DUTCH", comparisonType: StringComparison.Ordinal))
        {
            return ("nl-NL", 0x0413, requestedCodePage ?? 1252);
        }

        if (name.Contains(value: "FINNISH", comparisonType: StringComparison.Ordinal) || name.Contains(value: "SWEDISH", comparisonType: StringComparison.Ordinal))
        {
            return ("sv-SE", 0x041d, requestedCodePage ?? 1252);
        }

        if (name.Contains(value: "DANISH", comparisonType: StringComparison.Ordinal) || name.Contains(value: "NORWEGIAN", comparisonType: StringComparison.Ordinal))
        {
            return ("da-DK", 0x0406, requestedCodePage ?? 1252);
        }

        if (name.Contains(value: "ICELANDIC", comparisonType: StringComparison.Ordinal))
        {
            return ("is-IS", 0x040f, requestedCodePage ?? 1252);
        }

        if (name.Contains(value: "LATIN1_GENERAL", comparisonType: StringComparison.Ordinal))
        {
            return ("en-US", 0x0409, requestedCodePage ?? 1252);
        }

        throw new ArgumentException($"Unknown SQL Server collation '{name}'. Register explicit metadata with {nameof(RegisterSqlCollation)}.", nameof(name));
    }

    private static bool HasToken(string value, string token) =>
        value.Split(separator: '_', options: StringSplitOptions.RemoveEmptyEntries)
            .Contains(value: token, comparer: StringComparer.Ordinal);

    private static byte ResolveLegacySortId(string name)
    {
        if (!name.StartsWith(value: "SQL_", comparisonType: StringComparison.Ordinal))
        {
            return 0;
        }

        if (name.Contains(value: "CP1", comparisonType: StringComparison.Ordinal))
        {
            return 52;
        }

        return 0;
    }

    public int CountSqlCollationUtf8(string value) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationUtf8(value: value);
        return host.CountCodePageBytes(codePage: 65001, value: value);
    });

    public void WriteSqlCollationUtf8(Span<byte> destination, string value) =>
        TryCatch(operation: static context =>
    {
        ValidateSqlCollationUtf8Destination(destination: context.destination, value: context.value);
        host.WriteCodePage(codePage: 65001, value: context.value, destination: context.destination);
    }, context: new WritableSqlCollationTextContext { destination = destination, value = value });

    public string ReadSqlCollationUtf8(ReadOnlySpan<byte> value) =>
        TryCatch(operation: static context =>
    {
        ValidateSqlCollationUtf8Source(value: context.value);
        return host.DecodeCodePage(codePage: 65001, value: context.value);
    }, context: new ReadableSqlCollationContext { value = value });

    public void CheckSqlCollationName(string name) =>
        TryCatch(operation: () =>
    {
        ValidateSqlCollationName(name: name);
        _ = ParseCore(name: name);
    });
}