// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Indexes;
internal sealed record IndexPageLease
{
    public Guid Id { get; init; }
    public Memory<byte> Memory { get; init; }
}