// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Brokers.Masking;
internal interface ISqlMaskRegexBroker
{
    System.Collections.Generic.Dictionary<string, string>? Match(string source, string pattern, bool ignoreCase);
}