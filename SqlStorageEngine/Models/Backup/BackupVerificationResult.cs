// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Backup;
public sealed record BackupVerificationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get => _errors!; init => _errors = value; }

    private IReadOnlyList<string>? _errors;
}