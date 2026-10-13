// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlMaskEntropies;
internal sealed partial class SqlMaskEntropyService : ISqlMaskEntropyService
{
    private readonly sql_storage_engine.Brokers.Masking.SqlMaskRandomBroker random = new sql_storage_engine.Brokers.Masking.SqlMaskRandomBroker();
    public SqlMaskContext ReadSqlMaskContext(SqlMaskContext sqlMaskContext) =>
        TryCatch(operation: () =>
    {
        ValidateReadSqlMaskContext(sqlMaskContext: sqlMaskContext);
        long entropy;

        if (sqlMaskContext.Kind == "random")
        {
            entropy = sqlMaskContext.End == long.MaxValue ? random.NextInt64(minimum: sqlMaskContext.Start, maximum: sqlMaskContext.End) + random.Next(minimum: 0, maximum: 2) : random.NextInt64(minimum: sqlMaskContext.Start, maximum: sqlMaskContext.End + 1);
        }
        else
        {
            entropy = sqlMaskContext.Part switch
            {
                'Y' => random.Next(minimum: 1, maximum: 10_000),
                'M' => random.Next(minimum: 1, maximum: 13),
                'D' => random.Next(minimum: 1, maximum: DateTime.DaysInMonth(year: sqlMaskContext.DateTime.Year, month: sqlMaskContext.DateTime.Month) + 1),
                'h' => random.Next(minimum: 0, maximum: 24),
                'm' or 's' => random.Next(minimum: 0, maximum: 60),
                _ => throw new ArgumentException(message: "Unknown datetime mask component.", paramName: "part")};
        }

        return sqlMaskContext with
        {
            Entropy = entropy
        };
    });
}