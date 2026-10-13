// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Storage;
public sealed record StorageTableStatistics
{
    public TableId TableId { get; set; }
    public long RowCount { get; set; }
    public int HeapPageCount { get; set; }

    private IReadOnlyList<StorageColumnStatistics>? columns;
    public IReadOnlyList<StorageColumnStatistics> Columns { get => columns!; set => columns = value; }
    public DateTimeOffset CapturedAt { get; set; }
}