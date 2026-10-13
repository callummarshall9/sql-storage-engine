// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace sql_storage_engine.Dependencies.Masking;
internal sealed class SqlMaskRegexDependency : IDisposable
{
    private readonly ConcurrentDictionary<string, Regex> cache = new ConcurrentDictionary<string, Regex>(comparer: StringComparer.Ordinal);
    private bool disposed;
    public Dictionary<string, string>? Match(string source, string pattern, bool ignoreCase)
    {
        ObjectDisposedException.ThrowIf(condition: disposed, instance: this);
        var key = pattern + ignoreCase;
        var expression = cache.GetOrAdd(key: key, valueFactory: _ => new Regex(pattern: pattern, options: RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)));
        var match = expression.Match(input: source);

        if (!match.Success)
            return null;

        var groups = new Dictionary<string, string>(comparer: StringComparer.Ordinal);

        foreach (var name in expression.GetGroupNames())
            groups[name] = match.Groups[name].Value;

        return groups;
    }

    public void Dispose()
    {
        disposed = true;
        cache.Clear();
    }
}