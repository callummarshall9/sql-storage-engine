// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Services.Aggregations.StorageSessions;

namespace sql_storage_engine.Exposures.Catalog;
public sealed class CatalogSessionExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private readonly StorageSessionAggregationService service;
    private CatalogSessionExposure(StorageSessionAggregationService service)
    {
        this.service = service;
    }

    public CatalogSessionExposure(StorageSessionBinding storageSessionBinding)
    {
        service = (StorageSessionAggregationService)storageSessionBinding.Service!;
    }

    public PageId? ReadRootPageId() =>
        service.ReadStorageSessionContextCatalog(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext() })
            .Catalog!.RootPageId;

    public IReadOnlyList<CatalogTable> ReadCatalogTables() =>
        service.ReadStorageSessionContextCatalog(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext() })
            .Catalog!.Definition!.Tables;

    public IReadOnlyList<CatalogIndex> ReadCatalogIndexes() =>
        service.ReadStorageSessionContextCatalog(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext() })
            .Catalog!.Definition!.Indexes;

    public IReadOnlyList<CatalogScalarType> ReadCatalogScalarTypes() =>
        service.ReadStorageSessionContextCatalog(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext() })
            .Catalog!.Definition!.ScalarTypes;

    public IReadOnlyList<CatalogTableType> ReadCatalogTableTypes() =>
        service.ReadStorageSessionContextCatalog(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext() })
            .Catalog!.Definition!.TableTypes;

    public IReadOnlyList<SqlXmlSchemaCollection> ReadSqlXmlSchemaCollections() =>
        service.ReadStorageSessionContextCatalog(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext() })
            .Catalog!.Definition!.XmlSchemaCollections;

    public IReadOnlyList<CatalogAssembly> ReadCatalogAssemblies() =>
        service.ReadStorageSessionContextCatalog(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext() })
            .Catalog!.Definition!.Assemblies;

    internal StorageSecurityState ReadStorageSecurityState() =>
        service.ReadStorageSessionContextCatalog(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext() })
            .Catalog!.Definition!.Security;

    public StorageSessionBinding ReadStorageSessionBinding() =>
        service.ReadStorageSessionContextCatalog(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext() })
            .Binding!;

    internal static CatalogSessionExposure CreateCatalogSessionExposure(StorageSessionConfiguration configuration) =>
        new CatalogSessionExposure(service: StorageSessionAggregationService.CreateStorageSessionAggregationService(configuration: configuration));

    internal static ValueTask<CatalogSessionExposure> OpenCatalogSessionExposureAsync(PageId pageId, StorageSessionConfiguration configuration, CancellationToken cancellationToken = default) =>
        CreateCatalogSessionExposure(configuration: configuration)
            .OpenSourceAsync(pageId: pageId, cancellationToken: cancellationToken);

    private async ValueTask<CatalogSessionExposure> OpenSourceAsync(PageId pageId, CancellationToken cancellationToken)
    {
        await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 100, RootPageId = pageId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return this;
    }

    public async ValueTask<CatalogTable> CreateCatalogTableAsync(string name, ulong schemaVersion, IEnumerable<CatalogColumn> columns, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 1, Name = name, SchemaVersion = schemaVersion, Columns = columns } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.TableResult!;

    public async ValueTask<CatalogTable> CreateCatalogTableAsync(string name, ulong schemaVersion, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 2, Name = name, SchemaVersion = schemaVersion, Columns = columns, CheckConstraints = checkConstraints } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.TableResult!;

    public async ValueTask<CatalogTable> CreateCatalogTableAsync(CatalogTableName catalogTableName, ulong schemaVersion, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 3, QualifiedName = catalogTableName, SchemaVersion = schemaVersion, Columns = columns, CheckConstraints = checkConstraints } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.TableResult!;

    public async ValueTask<CatalogTable> CreateSystemVersionedCatalogTableAsync(CatalogTableName? [] catalogTableNames, ulong schemaVersion, IEnumerable<CatalogColumn> columns, ColumnId[] columnIds, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 4, QualifiedNames = catalogTableNames, SchemaVersion = schemaVersion, Columns = columns, PeriodColumnIds = columnIds, CheckConstraints = checkConstraints } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.TableResult!;

    public async ValueTask<SqlValue> AllocateIdentityAsync(TableId tableId, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 5, TableId = tableId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.ValueResult!;

    public bool TryOpenTable(string name, out CatalogTable? catalogTable)
    {
        var result = service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 6, Name = name } }, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Catalog!;

        catalogTable = result.Table;
        return result.Found;
    }

    public bool TryOpenTable(CatalogTableName catalogTableName, out CatalogTable? catalogTable)
    {
        var result = service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 7, QualifiedName = catalogTableName } }, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Catalog!;

        catalogTable = result.Table;
        return result.Found;
    }

    public bool TryOpenTable(TableId tableId, out CatalogTable? catalogTable)
    {
        var result = service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 8, TableId = tableId } }, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Catalog!;

        catalogTable = result.Table;
        return result.Found;
    }

    public async ValueTask<CatalogTable> RegisterGraphNodeTableAsync(TableId tableId, ColumnId columnId, IndexId indexId, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 9, TableId = tableId, IdentityColumnId = columnId, IdentityIndexId = indexId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.TableResult!;

    public async ValueTask<CatalogTable> RegisterGraphEdgeTableAsync(TableId tableId, CatalogGraphTable catalogGraphTable, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 102, TableId = tableId, Graph = catalogGraphTable } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.TableResult!;

    public async ValueTask<TableHeapBinding> OpenTableHeapBindingAsync(CatalogTable catalogTable, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 11, CatalogTableTable = catalogTable } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.HeapResult!;

    public async ValueTask<CatalogIndex> CreateCatalogIndexAsync(string name, TableId tableId, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 12, Name = name, TableId = tableId, IsUnique = isUnique, IndexedColumns = columns } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.IndexResult!;

    public async ValueTask<CatalogIndex> CreateCatalogIndexAsync(string name, TableId tableId, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CatalogBTreeIndexOptions catalogBTreeIndexOptions, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 13, Name = name, TableId = tableId, IsUnique = isUnique, IndexedColumns = columns, Options = catalogBTreeIndexOptions } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.IndexResult!;

    public async ValueTask<CatalogIndex> CreateSpecializedCatalogIndexAsync(string name, TableId tableId, CatalogIndexedColumn catalogIndexedColumn, CatalogSpecializedIndexOptions catalogSpecializedIndexOptions, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 14, Name = name, TableId = tableId, Column = catalogIndexedColumn, CatalogSpecializedIndexOptionsOptions = catalogSpecializedIndexOptions } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.IndexResult!;

    public bool TryOpenIndex(string name, TableId tableId, out CatalogIndex? catalogIndex)
    {
        var result = service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 15, Name = name, TableId = tableId } }, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Catalog!;

        catalogIndex = result.Index;
        return result.Found;
    }

    public async ValueTask<CatalogScalarType> CreateCatalogScalarTypeAsync(SqlType sqlType, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 16, TypeDefinition = sqlType } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.ScalarTypeResult!;

    public async ValueTask<CatalogTableType> CreateCatalogTableTypeAsync(string schemaName, string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes = null, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 17, SchemaName = schemaName, Name = name, Columns = columns, TableTypeIndexes = indexes } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.TableTypeResult!;

    public async ValueTask<CatalogTableType> CreateCatalogTableTypeAsync(string schemaName, string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes, IEnumerable<CatalogCheckConstraint>? checkConstraints, bool isMemoryOptimized, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 18, SchemaName = schemaName, Name = name, Columns = columns, TableTypeIndexes = indexes, CheckConstraints = checkConstraints, IsMemoryOptimized = isMemoryOptimized } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.TableTypeResult!;

    public bool TryOpenScalarType(string schemaName, string name, out CatalogScalarType? catalogScalarType)
    {
        var result = service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 19, SchemaName = schemaName, Name = name } }, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Catalog!;

        catalogScalarType = result.Type;
        return result.Found;
    }

    public bool TryOpenTableType(string schemaName, string name, out CatalogTableType? catalogTableType)
    {
        var result = service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 20, SchemaName = schemaName, Name = name } }, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Catalog!;

        catalogTableType = result.CatalogTableTypeType;
        return result.Found;
    }

    public bool TryOpenXmlSchemaCollection(string schemaName, string name, out SqlXmlSchemaCollection? sqlXmlSchemaCollection)
    {
        var result = service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 21, SchemaName = schemaName, Name = name } }, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Catalog!;

        sqlXmlSchemaCollection = result.Collection;
        return result.Found;
    }

    public bool TryOpenAssembly(string name, out CatalogAssembly? catalogAssembly)
    {
        var result = service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 22, Name = name } }, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Catalog!;

        catalogAssembly = result.Assembly;
        return result.Found;
    }

    public async ValueTask<SqlXmlSchemaCollection> CreateSqlXmlSchemaCollectionAsync(SqlXmlSchemaCollection sqlXmlSchemaCollection, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 23, SqlXmlSchemaCollectionCollection = sqlXmlSchemaCollection } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.XmlSchemaCollectionResult!;

    public async ValueTask<SqlXmlSchemaCollection> AlterXmlSchemaCollectionAsync(string schemaName, string name, IEnumerable<string> additionalDefinitions, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 24, SchemaName = schemaName, Name = name, AdditionalDefinitions = additionalDefinitions } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.XmlSchemaCollectionResult!;

    public ValueTask DropXmlSchemaCollectionAsync(string schemaName, string name, CancellationToken cancellationToken = default) =>
        IgnoreResultAsync(operation: service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 25, SchemaName = schemaName, Name = name } }, cancellationToken: cancellationToken));

    public async ValueTask<CatalogAssembly> CreateCatalogAssemblyAsync(CatalogAssembly catalogAssembly, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 26, CatalogAssemblyAssembly = catalogAssembly } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Catalog!.AssemblyResult!;

    public ValueTask DropAssemblyAsync(string name, CancellationToken cancellationToken = default) =>
        IgnoreResultAsync(operation: service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 27, Name = name } }, cancellationToken: cancellationToken));

    public IndexTreeBinding ReadIndexTreeBinding(CatalogIndex catalogIndex) =>
        service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 28, CatalogIndexIndex = catalogIndex } }, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Catalog!.TreeResult!;

    internal ValueTask WriteSecurityAsync(StorageSecurityState storageSecurityState, CancellationToken token) =>
        IgnoreResultAsync(operation: service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 29, State = storageSecurityState } }, cancellationToken: token));

    private static ValueTask IgnoreResultAsync(ValueTask<StorageSessionContext> operation) =>
        new ValueTask(task: operation.AsTask());

    internal async ValueTask<TableStorageBinding> OpenTableStorageBindingAsync(CatalogTable catalogTable, CancellationToken cancellationToken) =>
        ProjectTableStorageBinding(storageSessionContext: await service.ExecuteStorageSessionContextCatalogAsync(storageSessionContext: new StorageSessionContext { Catalog = new CatalogSessionContext { Operation = 101, Table = catalogTable } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

    private static TableStorageBinding ProjectTableStorageBinding(StorageSessionContext storageSessionContext) =>
        new TableStorageBinding
    {
        Service = storageSessionContext.Binding!.Service,
        Context = storageSessionContext.Table
    };
}