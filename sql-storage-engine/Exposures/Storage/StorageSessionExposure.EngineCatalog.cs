// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Exposures.Storage;
public sealed partial class StorageSessionExposure : IStorageCatalog
{
    public DatabaseId ReadDatabaseId() =>
        service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 7 } })
            .EngineCatalog!.DatabaseId;

    public string ReadDefaultCollation() =>
        service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 7 } })
            .EngineCatalog!.DefaultCollation!;

    public IReadOnlyList<CatalogTable> ReadTables() =>
        service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 0 } })
            .EngineCatalog!.Definition!.Tables.ToArray();

    public IReadOnlyList<CatalogIndex> ReadIndexes() =>
        service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 0 } })
            .EngineCatalog!.Definition!.Indexes.ToArray();

    public IReadOnlyList<CatalogScalarType> ReadScalarTypes() =>
        service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 0 } })
            .EngineCatalog!.Definition!.ScalarTypes.ToArray();

    public IReadOnlyList<CatalogTableType> ReadTableTypes() =>
        service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 0 } })
            .EngineCatalog!.Definition!.TableTypes.ToArray();

    public IReadOnlyList<SqlXmlSchemaCollection> ReadXmlSchemaCollections() =>
        service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 0 } })
            .EngineCatalog!.Definition!.XmlSchemaCollections.ToArray();

    public IReadOnlyList<CatalogAssembly> ReadAssemblies() =>
        service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 0 } })
            .EngineCatalog!.Definition!.Assemblies.ToArray();

    public IStorageCatalog ReadCatalog() =>
        new StorageSessionExposure(storageSessionContext: sessionContext);

    public bool TryGetTable(string name, out CatalogTable? catalogTable)
    {
        var result = service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 6, Name = name } } })
            .EngineCatalog!.Request!;

        catalogTable = result.Table;
        return result.Found;
    }

    public bool TryGetTable(CatalogTableName catalogTableName, out CatalogTable? catalogTable)
    {
        var result = service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 7, QualifiedName = catalogTableName } } })
            .EngineCatalog!.Request!;

        catalogTable = result.Table;
        return result.Found;
    }

    public bool TryGetTable(TableId tableId, out CatalogTable? catalogTable)
    {
        var result = service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 8, TableId = tableId } } })
            .EngineCatalog!.Request!;

        catalogTable = result.Table;
        return result.Found;
    }

    public bool TryGetIndex(TableId tableId, string name, out CatalogIndex? catalogIndex)
    {
        var result = service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 15, TableId = tableId, Name = name } } })
            .EngineCatalog!.Request!;

        catalogIndex = result.Index;
        return result.Found;
    }

    public bool TryGetScalarType(string schemaName, string name, out CatalogScalarType? catalogScalarType)
    {
        var result = service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 19, SchemaName = schemaName, Name = name } } })
            .EngineCatalog!.Request!;

        catalogScalarType = result.Type;
        return result.Found;
    }

    public bool TryGetTableType(string schemaName, string name, out CatalogTableType? catalogTableType)
    {
        var result = service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 20, SchemaName = schemaName, Name = name } } })
            .EngineCatalog!.Request!;

        catalogTableType = result.CatalogTableTypeType;
        return result.Found;
    }

    public bool TryGetXmlSchemaCollection(string schemaName, string name, out SqlXmlSchemaCollection? sqlXmlSchemaCollection)
    {
        var result = service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 21, SchemaName = schemaName, Name = name } } })
            .EngineCatalog!.Request!;

        sqlXmlSchemaCollection = result.Collection;
        return result.Found;
    }

    public bool TryGetAssembly(string name, out CatalogAssembly? catalogAssembly)
    {
        var result = service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 22, Name = name } } })
            .EngineCatalog!.Request!;

        catalogAssembly = result.Assembly;
        return result.Found;
    }

    public bool TryGetTemporalHistory(TableId tableId, out CatalogTable? catalogTable)
    {
        var result = service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 3, TableId = tableId } })
            .EngineCatalog!;

        catalogTable = result.Table;
        return result.Found;
    }

    public bool TryGetTemporalCurrent(TableId tableId, out CatalogTable? catalogTable)
    {
        var result = service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 4, TableId = tableId } })
            .EngineCatalog!;

        catalogTable = result.Table;
        return result.Found;
    }

    public IReadOnlyList<CatalogIndex> GetIndexes(TableId tableId) =>
        service.ReadStorageSessionContextEngineCatalog(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 2, TableId = tableId } })
            .EngineCatalog!.Indexes!;

    public async ValueTask<CatalogTable> CreateCatalogTableAsync(string name, IEnumerable<CatalogColumn> columns, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 1, Name = name, Columns = columns } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.TableResult!;

    public async ValueTask<CatalogTable> CreateCatalogTableAsync(string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint> checkConstraints, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 2, Name = name, Columns = columns, CheckConstraints = checkConstraints } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.TableResult!;

    public async ValueTask<CatalogTable> CreateCatalogTableAsync(CatalogTableName catalogTableName, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 3, QualifiedName = catalogTableName, Columns = columns, CheckConstraints = checkConstraints } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.TableResult!;

    public async ValueTask<CatalogTable> CreateSystemVersionedCatalogTableAsync(CatalogTableName? [] catalogTableNames, IEnumerable<CatalogColumn> columns, ColumnId[] columnIds, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 4, QualifiedNames = catalogTableNames, PeriodColumnIds = columnIds, Columns = columns, CheckConstraints = checkConstraints } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.TableResult!;

    public async ValueTask<CatalogIndex> CreateCatalogIndexAsync(string name, TableId tableId, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 12, Name = name, TableId = tableId, IsUnique = isUnique, IndexedColumns = columns } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.IndexResult!;

    public async ValueTask<CatalogIndex> CreateCatalogIndexAsync(string name, TableId tableId, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CatalogBTreeIndexOptions catalogBTreeIndexOptions, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 13, Name = name, TableId = tableId, IsUnique = isUnique, IndexedColumns = columns, Options = catalogBTreeIndexOptions } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.IndexResult!;

    public async ValueTask<CatalogIndex> CreateSpecializedCatalogIndexAsync(string name, TableId tableId, CatalogIndexedColumn catalogIndexedColumn, CatalogSpecializedIndexOptions catalogSpecializedIndexOptions, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 14, Name = name, TableId = tableId, Column = catalogIndexedColumn, CatalogSpecializedIndexOptionsOptions = catalogSpecializedIndexOptions } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.IndexResult!;

    public async ValueTask<CatalogTable> RegisterGraphNodeTableAsync(TableId tableId, ColumnId columnId, IndexId indexId, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 9, TableId = tableId, IdentityColumnId = columnId, IdentityIndexId = indexId } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.TableResult!;

    public async ValueTask<CatalogTable> RegisterGraphEdgeTableAsync(TableId tableId, CatalogGraphTable catalogGraphTable, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 102, TableId = tableId, Graph = catalogGraphTable } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.TableResult!;

    public async ValueTask<CatalogScalarType> CreateCatalogScalarTypeAsync(SqlType sqlType, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 16, TypeDefinition = sqlType } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.ScalarTypeResult!;

    public async ValueTask<CatalogTableType> CreateCatalogTableTypeAsync(string schemaName, string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes = null, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 17, SchemaName = schemaName, Name = name, Columns = columns, TableTypeIndexes = indexes } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.TableTypeResult!;

    public async ValueTask<CatalogTableType> CreateCatalogTableTypeAsync(string schemaName, string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes, IEnumerable<CatalogCheckConstraint>? checkConstraints, bool isMemoryOptimized, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 18, SchemaName = schemaName, Name = name, Columns = columns, TableTypeIndexes = indexes, CheckConstraints = checkConstraints, IsMemoryOptimized = isMemoryOptimized } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.TableTypeResult!;

    public async ValueTask<SqlXmlSchemaCollection> CreateSqlXmlSchemaCollectionAsync(SqlXmlSchemaCollection sqlXmlSchemaCollection, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 23, SqlXmlSchemaCollectionCollection = sqlXmlSchemaCollection } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.XmlSchemaCollectionResult!;

    public async ValueTask<SqlXmlSchemaCollection> AlterXmlSchemaCollectionAsync(string schemaName, string name, IEnumerable<string> additionalDefinitions, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 24, SchemaName = schemaName, Name = name, AdditionalDefinitions = additionalDefinitions } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.XmlSchemaCollectionResult!;

    public ValueTask DropXmlSchemaCollectionAsync(string schemaName, string name, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 25, SchemaName = schemaName, Name = name } } }, cancellationToken: cancellationToken)
            .AsTask());

    public async ValueTask<CatalogAssembly> CreateCatalogAssemblyAsync(CatalogAssembly catalogAssembly, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 26, CatalogAssemblyAssembly = catalogAssembly } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.Request!.AssemblyResult!;

    public ValueTask DropAssemblyAsync(string name, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 1, Request = new CatalogSessionContext { Operation = 27, Name = name } } }, cancellationToken: cancellationToken)
            .AsTask());

    public async ValueTask<StorageTableStatistics> GetTableStatisticsAsync(TableId tableId, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 5, TableId = tableId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.TableStatistics!;

    public async ValueTask<StorageIndexStatistics> GetIndexStatisticsAsync(IndexId indexId, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextEngineCatalogAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Statement = sessionContext.Statement, EngineCatalog = new EngineCatalogContext { Operation = 6, IndexId = indexId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).EngineCatalog!.IndexStatistics!;
}