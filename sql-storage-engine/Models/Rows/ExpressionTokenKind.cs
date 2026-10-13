// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Rows;
internal enum ExpressionTokenKind : byte
{
    End,
    Identifier,
    Number,
    String,
    Hex,
    LeftParen,
    RightParen,
    Comma,
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    And,
    Or,
    Not,
    Is,
    Null
}