// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Rows;
internal readonly record struct SqlExpressionToken
{
    public ExpressionTokenKind Kind { get; init; }

    private readonly string? text;
    public string Text { get => text!; init => text = value; }
}