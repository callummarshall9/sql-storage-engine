// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogPageChainRequest
{
    public bool IsWrite { get; set; }
    public byte[]? Payload { get; set; }
    public PageId RootPageId { get; set; }
    public IReadOnlyList<PageId>? PageIds { get; set; }
}