// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;

namespace sql_storage_engine.Storage;
/// <summary>Indicates that legacy indexed vector data produced a non-finite nearest-neighbor distance.</summary>
public sealed class NonFiniteVectorDistanceException : StorageException
{
    public NonFiniteVectorDistanceException(string indexName, RowId rowId) : base($"Vector index '{indexName}' produced a non-finite distance for row {rowId}.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: indexName);
        IndexName = indexName;
        RowId = rowId;
    }

    public string IndexName { get; }
    public RowId RowId { get; }
}