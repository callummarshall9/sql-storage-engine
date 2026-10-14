// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using System;

namespace sql_storage_engine.Models.Logging;
public sealed record PhysicalPageChange
{
    public PageId PageId { get; init; }
    public PageType PageType { get; init; }
    public ReadOnlyMemory<byte> BeforeImage { get; init; }
    public ReadOnlyMemory<byte> AfterImage { get; init; }
}