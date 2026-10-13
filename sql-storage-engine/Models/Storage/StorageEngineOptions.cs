// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Storage;
public sealed record StorageEngineOptions
{
    public int PageSize { get; init; }
    public int BufferPoolCapacity { get; init; }
    public int InlineValueThreshold { get; init; }
    public string DefaultCollation { get => defaultCollation!; init => defaultCollation = value; }
    public TimeProvider TimeProvider { get => timeProvider!; init => timeProvider = value; }

    private string? defaultCollation;
    private TimeProvider? timeProvider;
}