// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Indexes;
/// <summary>An immutable key with one byte per UTF-16 code unit and exact record value equality.</summary>
public sealed record IndexKey
{
    public string Content { get => content!; init => content = value; }

    private string? content;
}