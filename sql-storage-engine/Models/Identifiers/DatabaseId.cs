// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Identifiers;
public readonly record struct DatabaseId
{
    public Guid Value { get; init; }
}