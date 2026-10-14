// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Diagnostics;
public sealed record IntegrityFinding
{
    public string Code { get => _code!; init => _code = value; }

    private string? _code;
    public PageId? PageId { get; init; }
    public string Message { get => _message!; init => _message = value; }

    private string? _message;
}