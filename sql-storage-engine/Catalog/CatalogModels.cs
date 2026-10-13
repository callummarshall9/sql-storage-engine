// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Catalog;
/// <summary>Defines whether an indexed column is ordered in ascending or descending order.</summary>
public enum SortDirection : byte
{
    Ascending = 1,
    Descending = 2
}

/// <summary>Defines where SQL NULL values sort relative to non-NULL values.</summary>
public enum NullSortOrder : byte
{
    First = 1,
    Last = 2
}

public enum CatalogIndexStorageKind : byte
{
    Clustered = 1,
    NonClustered = 2,
    Hash = 3
}

public enum CatalogIndexMethod : byte
{
    BTree = 1,
    Json = 2,
    Spatial = 3,
    Vector = 4,
    Xml = 5,
    FullText = 6
}

public enum SqlVectorDistanceMetric : byte
{
    Cosine = 1,
    Euclidean = 2,
    DotProduct = 3
}

public enum CatalogXmlIndexKind : byte
{
    Primary = 1,
    Path = 2,
    Value = 3,
    Property = 4,
    Selective = 5
}

public enum CatalogAssemblyPermissionSet : byte
{
    Safe = 1,
    ExternalAccess = 2,
    Unsafe = 3
}

public enum CatalogGeneratedAlwaysKind : byte
{
    None = 0,
    RowStart = 1,
    RowEnd = 2,
    TransactionIdStart = 3,
    TransactionIdEnd = 4,
    SequenceNumberStart = 5,
    SequenceNumberEnd = 6,
    GraphIdentity = 7,
    GraphFromNode = 8,
    GraphToNode = 9
}

public enum CatalogEncryptionType : byte
{
    Deterministic = 1,
    Randomized = 2
}
/// <summary>A stable catalog record for one table column.</summary>
 
/// <summary>A primary, unique, or secondary key declared by a user-defined table type.</summary>
 
/// <summary>A database-scoped user-defined table type used by table-valued parameters and variables.</summary>
 
/// <summary>Validates the complete set of authoritative table and index records.</summary>