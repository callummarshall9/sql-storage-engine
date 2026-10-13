// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Security;
public sealed record StorageObjectBinding
{
    public sql_storage_engine.Models.Identifiers.DatabaseId DatabaseId { get; init; }
    public Guid ObjectId { get; init; }
    public ulong SchemaVersion { get; init; }
}