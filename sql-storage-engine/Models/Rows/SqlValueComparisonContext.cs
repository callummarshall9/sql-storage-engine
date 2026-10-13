// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
internal sealed record SqlValueComparisonContext
{
    public SqlValue? Left { get; init; }
    public SqlValue? Right { get; init; }
    public SqlComparison Comparison { get; init; }
    public int Order { get; init; }
    public int VariantFamily { get; init; }
    public SqlValue? Result { get; init; }
    public string? Text { get; init; }
    public SqlDecimal Exact { get; init; }
    public ExpressionTokenKind Operator { get; init; }
}