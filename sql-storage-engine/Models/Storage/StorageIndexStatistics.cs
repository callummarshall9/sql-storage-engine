// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Storage;
public sealed record StorageIndexStatistics
{
    public IndexId IndexId { get; set; }
    public long EntryCount { get; set; }
    public long DistinctKeyCount { get; set; }
    public int LeafPageCount { get; set; }

    private IReadOnlyList<StorageHistogramBucket>? histogram;
    public IReadOnlyList<StorageHistogramBucket> Histogram { get => histogram!; set => histogram = value; }
    public DateTimeOffset CapturedAt { get; set; }
}