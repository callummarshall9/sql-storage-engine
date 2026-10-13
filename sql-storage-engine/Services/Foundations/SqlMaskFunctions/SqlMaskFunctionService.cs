// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Globalization;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlMaskFunctions;
internal sealed partial class SqlMaskFunctionService : ISqlMaskFunctionService
{
    private readonly sql_storage_engine.Brokers.Masking.SqlMaskRegexBroker patterns = new sql_storage_engine.Brokers.Masking.SqlMaskRegexBroker();
    private const string PartialPattern = "^partial\\(\\s*(?<prefix>\\d+)\\s*,\\s*(?<quote>['\"])(?<padding>.*)\\k<quote>\\s*,\\s*(?<suffix>\\d+)\\s*\\)$";
    private const string RandomPattern = "^random\\(\\s*(?<start>-?\\d+)\\s*,\\s*(?<end>-?\\d+)\\s*\\)$";
    private const string DateTimePattern = "^datetime\\(\\s*[\"'](?<part>[YMDhms])[\"']\\s*\\)$";
    public SqlMaskContext ReadSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidateReadSqlMaskContext(sqlMaskContext: sqlMaskContext);
        var function = sqlMaskContext.Function!.Trim();

        if (function.Equals(value: "default()", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return sqlMaskContext with
            {
                Kind = "default"
            };
        }

        if (function.Equals(value: "email()", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return sqlMaskContext with
            {
                Kind = "email"
            };
        }

        var partial = patterns.Match(source: function, pattern: PartialPattern, ignoreCase: true);

        if (partial is not null)
        {
            var quote = partial["quote"];

            return sqlMaskContext with
            {
                Kind = "partial",
                Prefix = int.Parse(s: partial["prefix"], provider: CultureInfo.InvariantCulture),
                Suffix = int.Parse(s: partial["suffix"], provider: CultureInfo.InvariantCulture),
                Padding = partial["padding"].Replace(oldValue: quote + quote, newValue: quote, comparisonType: StringComparison.Ordinal)
            };
        }

        var random = patterns.Match(source: function, pattern: RandomPattern, ignoreCase: true);

        if (random is not null)
        {
            return sqlMaskContext with
            {
                Kind = "random",
                Start = long.Parse(s: random["start"], provider: CultureInfo.InvariantCulture),
                End = long.Parse(s: random["end"], provider: CultureInfo.InvariantCulture)
            };
        }

        var dateTime = patterns.Match(source: function, pattern: DateTimePattern, ignoreCase: false);

        return sqlMaskContext with
        {
            Kind = dateTime is null ? "unknown" : "datetime",
            Part = dateTime is null ? default : dateTime["part"][0]
        };
    });
}