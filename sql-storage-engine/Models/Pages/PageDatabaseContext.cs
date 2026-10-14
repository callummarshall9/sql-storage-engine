// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Pages;

namespace sql_storage_engine.Models.Pages;
internal sealed record PageDatabaseContext
{
    public PageDatabaseRuntime? Runtime { get; init; }
    public string? Path { get; init; }
    public string? TemporaryPath { get; init; }
    public int PageSize { get; init; }
    public DatabaseOpenMode OpenMode { get; init; }
    public Guid DatabaseIdentity { get; init; }
    public ulong ResultPageId { get; init; }
}