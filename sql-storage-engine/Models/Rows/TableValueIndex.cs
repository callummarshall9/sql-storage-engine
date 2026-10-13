// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Models.Rows;
internal sealed record TableValueIndex
{
    public CatalogTableTypeIndex Definition { get => definition!; init => definition = value; }
    public CatalogIndex CatalogIndex { get => catalogIndex!; init => catalogIndex = value; }
    public HashSet<IndexKey> Keys { get => keys!; init => keys = value; }

    private CatalogTableTypeIndex? definition;
    private CatalogIndex? catalogIndex;
    private HashSet<IndexKey>? keys;
}