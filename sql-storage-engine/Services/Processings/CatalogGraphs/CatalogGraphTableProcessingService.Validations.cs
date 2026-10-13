// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Processings.CatalogGraphs;
internal sealed partial class CatalogGraphTableProcessingService
{
    private static void ValidateCatalogGraphTable(CatalogGraphTable catalogGraphTable)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogGraphTable);
        var kind = catalogGraphTable.Kind;
        var identityColumnId = catalogGraphTable.IdentityColumnId;
        var identityIndexId = catalogGraphTable.IdentityIndexId;
        var fromNodeTableId = catalogGraphTable.FromNodeTableId;
        var toNodeTableId = catalogGraphTable.ToNodeTableId;
        var fromNodeColumnId = catalogGraphTable.FromNodeColumnId;
        var toNodeColumnId = catalogGraphTable.ToNodeColumnId;
        var outgoingIndexId = catalogGraphTable.OutgoingIndexId;
        var incomingIndexId = catalogGraphTable.IncomingIndexId;

        if (!Enum.IsDefined(value: kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (kind == GraphTableKind.Node && new object? []
        {
            fromNodeTableId,
            toNodeTableId,
            fromNodeColumnId,
            toNodeColumnId,
            outgoingIndexId,
            incomingIndexId
        }.Any(predicate: value => value is not null))
        {
            throw new ArgumentException("A graph node table cannot declare edge endpoint metadata.");
        }

        if (kind == GraphTableKind.Edge && new object? []
        {
            fromNodeTableId,
            toNodeTableId,
            fromNodeColumnId,
            toNodeColumnId,
            outgoingIndexId,
            incomingIndexId
        }.Any(predicate: value => value is null))
        {
            throw new ArgumentException("A graph edge table requires complete endpoint and adjacency metadata.");
        }

        if (kind == GraphTableKind.Edge && (identityColumnId == fromNodeColumnId || identityColumnId == toNodeColumnId || fromNodeColumnId == toNodeColumnId))
        {
            throw new ArgumentException("Graph identity and endpoint columns must be distinct.");
        }

        if (kind == GraphTableKind.Edge && (identityIndexId == outgoingIndexId || identityIndexId == incomingIndexId || outgoingIndexId == incomingIndexId))
        {
            throw new ArgumentException("Graph identity and adjacency indexes must be distinct.");
        }
    }
}