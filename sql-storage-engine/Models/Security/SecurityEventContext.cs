// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Security;
internal sealed record SecurityEventContext
{
    public string? TransactionPath { get; set; }
    public Guid DatabaseId { get; set; }
    public Action<string>? Observer { get; set; }
    public StorageAuditRecord? Record { get; set; }
    public IReadOnlyList<StorageAuditRecord>? Records { get; set; }
    public bool HasTransaction { get; set; }
    public bool Uncertain { get; set; }
}