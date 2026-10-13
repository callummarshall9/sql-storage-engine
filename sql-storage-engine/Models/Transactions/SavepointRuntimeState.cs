// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Transactions;
internal sealed record SavepointRuntimeState
{
    public List<StorageSavepointEntry> Entries { get => entries!; init => entries = value; }

    private List<StorageSavepointEntry>? entries;
    public long Bytes { get; set; }
}