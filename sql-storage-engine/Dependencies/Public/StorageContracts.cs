using sql_storage_engine.Models.Storage;
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine;
/// <summary>Read-only catalog operations intended for name binding and semantic analysis.</summary>
public interface IStorageCatalog
{
    DatabaseId ReadDatabaseId();

    IReadOnlyList<CatalogTable> ReadTables();

    IReadOnlyList<CatalogIndex> ReadIndexes();

    IReadOnlyList<CatalogScalarType> ReadScalarTypes();

    IReadOnlyList<CatalogTableType> ReadTableTypes();

    IReadOnlyList<SqlXmlSchemaCollection> ReadXmlSchemaCollections();

    IReadOnlyList<CatalogAssembly> ReadAssemblies();

    string ReadDefaultCollation();

    bool TryGetTable(string name, out CatalogTable? catalogTable);

    bool TryGetTable(CatalogTableName catalogTableName, out CatalogTable? catalogTable);

    bool TryGetTable(TableId tableId, out CatalogTable? catalogTable);

    bool TryGetTemporalHistory(TableId tableId, out CatalogTable? catalogTable);

    bool TryGetTemporalCurrent(TableId tableId, out CatalogTable? catalogTable);

    bool TryGetIndex(TableId tableId, string name, out CatalogIndex? catalogIndex);

    IReadOnlyList<CatalogIndex> GetIndexes(TableId tableId);

    bool TryGetScalarType(string schemaName, string name, out CatalogScalarType? catalogScalarType);

    bool TryGetTableType(string schemaName, string name, out CatalogTableType? catalogTableType);

    bool TryGetXmlSchemaCollection(string schemaName, string name, out SqlXmlSchemaCollection? sqlXmlSchemaCollection);

    bool TryGetAssembly(string name, out CatalogAssembly? catalogAssembly);
}

/// <summary>Logical table access for a SQL executor; physical pages and encodings remain hidden.</summary>
public interface IStorageTable
{
    CatalogTable ReadDefinition();

    ValueTask<RowId> InsertAsync(Row row, CancellationToken cancellationToken = default);

    ValueTask<TableInsertResult> TryInsertAsync(Row row, CancellationToken cancellationToken = default);

    ValueTask<StoredRow?> GetAsync(RowId rowId, CancellationToken cancellationToken = default);

    ValueTask<StoredRow?> GetAsync(RowId rowId, StorageReadOptions readOptions, CancellationToken cancellationToken = default);

    IAsyncEnumerable<StoredRow> ScanAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<StoredRow> ScanAsync(StorageReadOptions readOptions, CancellationToken cancellationToken = default);

    IAsyncEnumerable<StoredRow> SampleAsync(StorageTableSample sample, CancellationToken cancellationToken = default);

    IAsyncEnumerable<StoredRow> SampleAsync(StorageTableSample sample, StorageReadOptions readOptions, CancellationToken cancellationToken = default);

    IAsyncEnumerable<StorageTemporalRow> TemporalScanAsync(StorageTemporalQuery query, CancellationToken cancellationToken = default);

    IAsyncEnumerable<StorageTemporalRow> TemporalScanAsync(StorageTemporalQuery query, StorageReadOptions readOptions, CancellationToken cancellationToken = default);

    ValueTask<TableUpdateResult> UpdateAsync(RowId rowId, RowUpdate update, CancellationToken cancellationToken = default);

    ValueTask<TableDeleteResult> DeleteAsync(RowId rowId, CancellationToken cancellationToken = default);
}

/// <summary>Logical secondary-index access using typed values in the index's declared column order.</summary>
public interface IStorageIndex
{
    CatalogIndex ReadDefinition();

    ValueTask<IReadOnlyList<RowId>> FindAsync(IReadOnlyList<SqlValue> values, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<StorageIndexEntry>> FindEntriesAsync(IReadOnlyList<SqlValue> values, CancellationToken cancellationToken = default);

    IAsyncEnumerable<RowId> ScanAsync(IReadOnlyList<SqlValue> lowerBound, IReadOnlyList<SqlValue> upperBound, bool includeLowerBound = true, bool includeUpperBound = true, ScanDirection direction = ScanDirection.Ascending, CancellationToken cancellationToken = default);

    IAsyncEnumerable<RowId> ScanAsync(StorageIndexRange range, CancellationToken cancellationToken = default);

    IAsyncEnumerable<StorageIndexEntry> ScanEntriesAsync(StorageIndexRange range, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<SpecializedIndexMatch>> SearchNearestAsync(SqlValue query, int count, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<RowId>> FindJsonPathAsync(string path, SqlValue value, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<RowId>> FindJsonPathExistsAsync(string path, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<RowId>> FindXmlPathAsync(string path, string? value = null, CancellationToken cancellationToken = default);

    IAsyncEnumerable<FullTextIndexMatch> SearchFullTextAsync(FullTextSearchRequest request, CancellationToken cancellationToken = default);
}

/// <summary>The supported high-level boundary between a SQL engine and this storage package.</summary>
public interface IStorageEngine : IAsyncDisposable
{
    Security.IStorageSecurity ReadSecurity();

    ValueTask<IStorageTransaction> BeginTransactionAsync(StorageTransactionOptions options, CancellationToken cancellationToken = default);

    ValueTask<StorageTransactionReceipt> ResolveTransactionAsync(StorageTransactionIdentity identity, CancellationToken cancellationToken = default);

    ValueTask ReleaseTransactionReceiptAsync(StorageTransactionIdentity identity, CancellationToken cancellationToken = default);

    DatabaseId ReadDatabaseId();

    IStorageCatalog ReadCatalog();

    ValueTask<CatalogTable> CreateCatalogTableAsync(string name, IEnumerable<CatalogColumn> columns, CancellationToken cancellationToken = default);

    ValueTask<CatalogTable> CreateCatalogTableAsync(string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint> checkConstraints, CancellationToken cancellationToken = default);

    ValueTask<CatalogTable> CreateCatalogTableAsync(CatalogTableName catalogTableName, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, CancellationToken cancellationToken = default);

    ValueTask<CatalogTable> CreateSystemVersionedCatalogTableAsync(CatalogTableName? [] catalogTableNames, IEnumerable<CatalogColumn> columns, ColumnId[] columnIds, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, CancellationToken cancellationToken = default);

    ValueTask<CatalogIndex> CreateCatalogIndexAsync(string name, TableId tableId, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CancellationToken cancellationToken = default);

    ValueTask<CatalogIndex> CreateCatalogIndexAsync(string name, TableId tableId, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CatalogBTreeIndexOptions catalogBTreeIndexOptions, CancellationToken cancellationToken = default);

    ValueTask<CatalogIndex> CreateSpecializedCatalogIndexAsync(string name, TableId tableId, CatalogIndexedColumn catalogIndexedColumn, CatalogSpecializedIndexOptions catalogSpecializedIndexOptions, CancellationToken cancellationToken = default);

    ValueTask<CatalogTable> RegisterGraphNodeTableAsync(TableId tableId, ColumnId columnId, IndexId indexId, CancellationToken cancellationToken = default);

    ValueTask<CatalogTable> RegisterGraphEdgeTableAsync(TableId tableId, CatalogGraphTable catalogGraphTable, CancellationToken cancellationToken = default);

    ValueTask<CatalogScalarType> CreateCatalogScalarTypeAsync(SqlType sqlType, CancellationToken cancellationToken = default);

    ValueTask<CatalogTableType> CreateCatalogTableTypeAsync(string schemaName, string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes = null, CancellationToken cancellationToken = default);

    ValueTask<CatalogTableType> CreateCatalogTableTypeAsync(string schemaName, string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes, IEnumerable<CatalogCheckConstraint>? checkConstraints, bool isMemoryOptimized, CancellationToken cancellationToken = default);

    ValueTask<SqlXmlSchemaCollection> CreateSqlXmlSchemaCollectionAsync(SqlXmlSchemaCollection sqlXmlSchemaCollection, CancellationToken cancellationToken = default);

    ValueTask<SqlXmlSchemaCollection> AlterXmlSchemaCollectionAsync(string schemaName, string name, IEnumerable<string> additionalDefinitions, CancellationToken cancellationToken = default);

    ValueTask DropXmlSchemaCollectionAsync(string schemaName, string name, CancellationToken cancellationToken = default);

    ValueTask<CatalogAssembly> CreateCatalogAssemblyAsync(CatalogAssembly catalogAssembly, CancellationToken cancellationToken = default);

    ValueTask DropAssemblyAsync(string name, CancellationToken cancellationToken = default);

    ValueTask<IStorageTable> OpenTableAsync(TableId tableId, CancellationToken cancellationToken = default);

    ValueTask<IStorageIndex> OpenIndexAsync(IndexId indexId, CancellationToken cancellationToken = default);

    ValueTask<IStorageGraphNodeTable> OpenGraphNodeTableAsync(TableId tableId, CancellationToken cancellationToken = default);

    ValueTask<IStorageGraphEdgeTable> OpenGraphEdgeTableAsync(TableId tableId, CancellationToken cancellationToken = default);

    ValueTask<StorageTableStatistics> GetTableStatisticsAsync(TableId tableId, CancellationToken cancellationToken = default);

    ValueTask<StorageIndexStatistics> GetIndexStatisticsAsync(IndexId indexId, CancellationToken cancellationToken = default);

    ValueTask ExecuteStatementAsync(Func<IStorageStatement, CancellationToken, ValueTask> operation, CancellationToken cancellationToken = default);

    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}