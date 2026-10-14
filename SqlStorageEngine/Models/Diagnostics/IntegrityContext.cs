// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.Models.Diagnostics;
internal sealed record IntegrityContext
{
    public RecoveryPageContext? Source { get; set; }
    public DatabaseHeader? DatabaseHeader { get; set; }
    public IntegrityCrossCheck? CrossCheck { get; set; }
    public int MaximumPages { get; set; }
    public ulong PageId { get; set; }
    public byte[]? Page { get; set; }
    public PageHeader PageHeader { get; set; }
    public IntegrityReport? Report { get; set; }
}