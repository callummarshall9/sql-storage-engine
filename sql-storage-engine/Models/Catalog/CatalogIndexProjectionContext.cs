// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogIndexProjectionContext
{
    public int Operation { get; init; }
    public CatalogIndexDocumentContext? Document { get; set; }
    public string? Text { get; set; }
    public SqlCollation? Collation { get; set; }
    public CatalogColumnEncryption? Encryption { get; init; }
    public byte[]? Bytes { get; set; }
    public bool Unicode { get; init; }
    public CatalogFullTextIndexOptions? FullTextOptions { get; init; }
    public CatalogSpecializedIndexOptions? SpecializedOptions { get; set; }
    public SqlType? SqlType { get; init; }
    public SqlValue? SqlValue { get; set; }
    public string? ColumnName { get; init; }
    public SqlSpatial? Spatial { get; init; }
    public int NumberResult { get; set; }
    public bool BooleanResult { get; set; }
    public IReadOnlyList<string>? Tokens { get; set; }
    public (double MinX, double MinY, double MaxX, double MaxY) Bounds { get; set; }
    public TableDefinition? TableDefinition { get; set; }
}