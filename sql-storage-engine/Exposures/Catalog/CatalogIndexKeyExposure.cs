// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Managements.CatalogIndexKeys;

namespace sql_storage_engine.Exposures.Catalog;
public sealed class CatalogIndexKeyExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private CatalogIndexKeyExposure()
    {
    }

    private static readonly ICatalogIndexKeyRequestManagementService service = CatalogIndexKeyRequestManagementService.CreateCatalogIndexKeyRequestManagementService();
    public const int MaximumClusteredKeyBytes = 900;
    public const int MaximumNonClusteredKeyBytes = 1_700;
    public const int MaximumSqlVariantKeyBytes = 900;
    public static IReadOnlyList<sql_storage_engine.Models.Indexes.IndexKey> EncodeEntries(Row row, CatalogTable catalogTable, CatalogIndex catalogIndex) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 1, Row = row, Table = catalogTable, Index = catalogIndex })
            .Entries!;

    public static sql_storage_engine.Models.Indexes.IndexKey EncodeJsonPathValue(CatalogIndex catalogIndex, string path, SqlValue sqlValue) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 2, Index = catalogIndex, Path = path, SqlValue = sqlValue })
            .Key!;

    public static sql_storage_engine.Models.Indexes.IndexKey EncodeJsonPathExists(CatalogIndex catalogIndex, string path) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 3, Index = catalogIndex, Path = path })
            .Key!;

    public static sql_storage_engine.Models.Indexes.IndexKey EncodeXmlPathValue(CatalogIndex catalogIndex, string path, string value) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 4, Index = catalogIndex, Path = path, Value = value })
            .Key!;

    public static sql_storage_engine.Models.Indexes.IndexKey EncodeXmlPathExists(CatalogIndex catalogIndex, string path) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 5, Index = catalogIndex, Path = path })
            .Key!;

    public static sql_storage_engine.Models.Indexes.IndexKey EncodeFullTextTerm(CatalogIndex catalogIndex, CatalogTable catalogTable, FullTextSearchRequest request) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 6, Index = catalogIndex, Table = catalogTable, Request = request })
            .Key!;

    public static sql_storage_engine.Models.Indexes.IndexKey Encode(Row row, CatalogTable catalogTable, CatalogIndex catalogIndex) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 7, Row = row, Table = catalogTable, Index = catalogIndex })
            .Key!;

    public static byte[] EncodeIncludedValues(Row row, CatalogTable catalogTable, CatalogIndex catalogIndex) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 8, Row = row, Table = catalogTable, Index = catalogIndex })
            .PayloadResult!;

    public static IReadOnlyDictionary<ColumnId, SqlValue> DecodeIncludedValues(ReadOnlySpan<byte> payload, CatalogTable catalogTable, CatalogIndex catalogIndex) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 9, Payload = payload.ToArray(), Table = catalogTable, Index = catalogIndex })
            .IncludedValues!;

    public static sql_storage_engine.Models.Indexes.IndexKey EncodeValues(IReadOnlyList<SqlValue> values, CatalogTable catalogTable, CatalogIndex catalogIndex) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 10, Values = values, Table = catalogTable, Index = catalogIndex })
            .Key!;

    public static sql_storage_engine.Models.Indexes.IndexKey EncodePrefix(IReadOnlyList<SqlValue> values, CatalogTable catalogTable, CatalogIndex catalogIndex) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 11, Values = values, Table = catalogTable, Index = catalogIndex })
            .Key!;

    public static IReadOnlyList<IndexKey> EncodeJsonPathValueKeys(CatalogIndex catalogIndex, string path, SqlValue sqlValue) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 12, Index = catalogIndex, Path = path, SqlValue = sqlValue })
            .Entries!;

    public static IReadOnlyList<IndexKey> EncodeJsonPathExistsKeys(CatalogIndex catalogIndex, string path) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 13, Index = catalogIndex, Path = path })
            .Entries!;

    public static IndexKey EncodeValues(IReadOnlyList<SqlValue> values, CatalogTable catalogTable, CatalogIndex catalogIndex, int maximumKeyBytes) =>
        service.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 14, Values = values, Table = catalogTable, Index = catalogIndex, MaximumKeyBytes = maximumKeyBytes })
            .Key!;
}