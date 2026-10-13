// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Tables;
/// <summary>Reports whether a row was inserted or skipped by a unique index using IGNORE_DUP_KEY.</summary>
public sealed record TableInsertResult
{
    public bool Inserted { get; init; }
    public RowId? RowId { get; init; }
    public IReadOnlyList<string> Warnings { get => _warnings!; init => _warnings = value; }

    private IReadOnlyList<string>? _warnings;
}