// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Dependencies.Masking;

namespace sql_storage_engine.Brokers.Masking;
internal sealed class SqlMaskRegexBroker : ISqlMaskRegexBroker
{
    private readonly SqlMaskRegexDependency dependency = new SqlMaskRegexDependency();
    public System.Collections.Generic.Dictionary<string, string>? Match(string source, string pattern, bool ignoreCase) =>
        dependency.Match(source: source, pattern: pattern, ignoreCase: ignoreCase);
}