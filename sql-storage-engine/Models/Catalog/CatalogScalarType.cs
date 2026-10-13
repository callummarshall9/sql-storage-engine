// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogScalarType
{
    public SqlType Definition { get => definition!; init => definition = value; }

    private SqlType? definition;
}