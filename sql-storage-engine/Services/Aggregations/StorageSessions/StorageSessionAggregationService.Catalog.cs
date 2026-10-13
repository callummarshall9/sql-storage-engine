// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using System.Numerics;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private ValueTask<CatalogTable> CreateCatalogTableAsync(string name, ulong schemaVersion, IEnumerable<CatalogColumn> columns, CancellationToken cancellationToken = default) =>
        CreateCatalogTableAsync(name: name, schemaVersion: schemaVersion, columns: columns, checkConstraints: null, cancellationToken: cancellationToken);

    private ValueTask<CatalogTable> CreateCatalogTableAsync(string name, ulong schemaVersion, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints, CancellationToken cancellationToken = default) =>
        CreateCatalogTableAsync(name: CreateCatalogTableName(databaseName: "default", schemaName: "dbo", name: name), schemaVersion: schemaVersion, columns: columns, checkConstraints: checkConstraints, cancellationToken: cancellationToken);

    private async ValueTask<CatalogTable> CreateCatalogTableAsync(CatalogTableName name, ulong schemaVersion, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, CancellationToken cancellationToken = default)
    {
        name = PrepareCatalogTableName(catalogTableName: name);
        ArgumentNullException.ThrowIfNull(argument: columns);
        var columnSnapshot = columns.ToArray();
        var checkSnapshot = checkConstraints?.ToArray() ?? [];

        if (catalog.Definition.Tables.Any(predicate: table => table.QualifiedName == name))
        {
            throw new CatalogConflictException(message: $"A table named {FormatCatalogTableName(catalogTableName: name)} already exists.");
        }

        var nextId = new sql_storage_engine.Models.Identifiers.TableId
        {
            Value = catalog.Definition.Tables.Count == 0 ? 1UL : checked(catalog.Definition.Tables.Max(selector: table => table.Id.Value) + 1)
        };

        _ = CreateCatalogTable(tableId: nextId, name: name.Name, schemaVersion: schemaVersion, pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, columns: columnSnapshot, checkConstraints: checkSnapshot, databaseName: name.DatabaseName, schemaName: name.SchemaName);

        var heap = await CreateCatalogTableHeapBindingAsync(tableId: nextId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var table = CreateCatalogTable(tableId: nextId, name: name.Name, schemaVersion: schemaVersion, pageId: heap.RootPageId, columns: columnSnapshot, checkConstraints: checkSnapshot, databaseName: name.DatabaseName, schemaName: name.SchemaName);
        var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables.Append(element: table), indexes: catalog.Definition.Indexes, scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections, assemblies: catalog.Definition.Assemblies);

        try
        {
            var written = await WriteDefinitionAsync(candidate: candidate, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await FlushCatalogStorageAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            catalog.Definition = candidate;
            catalog.RootPageId = written.RootPageId;
            return table;
        }
        catch
        {
            catalog.HeapBindings!.Remove(key: nextId);

            await catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 14, Id = nextId.Value }, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

            await ReleaseCatalogPageAsync(pageId: heap.RootPageId, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);

            throw;
        }
    }

    private async ValueTask<CatalogTable> CreateSystemVersionedCatalogTableAsync(CatalogTableName name, CatalogTableName historyName, ulong schemaVersion, IEnumerable<CatalogColumn> columns, ColumnId periodStartColumnId, ColumnId periodEndColumnId, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, CancellationToken cancellationToken = default)
    {
        name = PrepareCatalogTableName(catalogTableName: name);
        historyName = PrepareCatalogTableName(catalogTableName: historyName);
        ArgumentNullException.ThrowIfNull(argument: columns);

        if (name == historyName)
        {
            throw new ArgumentException(message: "The current and history tables must have different names.", paramName: nameof(historyName));
        }

        if (catalog.Definition.Tables.Any(predicate: table => table.QualifiedName == name || table.QualifiedName == historyName))
        {
            throw new CatalogConflictException(message: "The current or history table name already exists.");
        }

        var columnSnapshot = columns.ToArray();
        var checkSnapshot = checkConstraints?.ToArray() ?? [];
        var nextValue = catalog.Definition.Tables.Count == 0 ? 1UL : checked(catalog.Definition.Tables.Max(selector: table => table.Id.Value) + 1);

        var currentId = new sql_storage_engine.Models.Identifiers.TableId
        {
            Value = nextValue
        };

        var historyId = new sql_storage_engine.Models.Identifiers.TableId
        {
            Value = checked(nextValue + 1)
        };

        var temporal = CreateCatalogSystemVersioning(tableId: historyId, periodStartColumnId: (periodStartColumnId).Value, periodEndColumnId: (periodEndColumnId).Value);
        _ = CreateCatalogTable(tableId: currentId, name: name.Name, schemaVersion: schemaVersion, pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, columns: columnSnapshot, checkConstraints: checkSnapshot, databaseName: name.DatabaseName, schemaName: name.SchemaName, catalogSystemVersioning: temporal);
        sql_storage_engine.Models.Heap.TableHeapBinding? currentHeap = null;
        sql_storage_engine.Models.Heap.TableHeapBinding? historyHeap = null;

        try
        {
            currentHeap = await CreateCatalogTableHeapBindingAsync(tableId: currentId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            historyHeap = await CreateCatalogTableHeapBindingAsync(tableId: historyId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            var current = CreateCatalogTable(tableId: currentId, name: name.Name, schemaVersion: schemaVersion, pageId: currentHeap.RootPageId, columns: columnSnapshot, checkConstraints: checkSnapshot, databaseName: name.DatabaseName, schemaName: name.SchemaName, catalogSystemVersioning: temporal);
            var history = CreateCatalogTable(tableId: historyId, name: historyName.Name, schemaVersion: schemaVersion, pageId: historyHeap.RootPageId, columns: columnSnapshot, databaseName: historyName.DatabaseName, schemaName: historyName.SchemaName);

            var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables.Append(element: current)
            .Append(element: history), indexes: catalog.Definition.Indexes, scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections, assemblies: catalog.Definition.Assemblies);

            var written = await WriteDefinitionAsync(candidate: candidate, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await FlushCatalogStorageAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            catalog.Definition = candidate;
            catalog.RootPageId = written.RootPageId;
            return current;
        }
        catch
        {
            foreach (var failedId in new[]
            {
                currentId,
                historyId
            }

            )
            {
                catalog.HeapBindings!.Remove(key: failedId);

                await catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 14, Id = failedId.Value }, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            foreach (var heap in new[]
            {
                historyHeap,
                currentHeap
            }.Where(predicate: heap => heap is not null))
            {
                await ReleaseCatalogPageAsync(pageId: heap!.RootPageId, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            throw;
        }
    }

    private async ValueTask<SqlValue> AllocateIdentityAsync(TableId tableId, CancellationToken cancellationToken = default)
    {
        await EnterCatalogMutationAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            if (!TryOpenTable(tableId: tableId, table: out var table))
            {
                throw new ArgumentException(message: "Unknown table ID.", paramName: nameof(tableId));
            }

            var identityColumn = table!.Columns.SingleOrDefault(predicate: column => column.Identity is not null) ?? throw new InvalidOperationException(message: "The table has no identity column.");
            var current = table.NextIdentityValue ?? throw new StorageCorruptionException(message: "Identity allocation state is missing.");
            var allocated = CreateAllocatedIdentitySqlValue(column: identityColumn, current: current);
            var next = current + identityColumn.Identity!.Increment;

            var replacement = CreateCatalogTable(tableId: table.Id, name: table.Name, schemaVersion: table.SchemaVersion, pageId: table.FirstHeapPageId, columns: table.Columns, checkConstraints: table.CheckConstraints, nextIdentityValue: next, databaseName: table.DatabaseName, schemaName: table.SchemaName, catalogSystemVersioning: table.SystemVersioning, catalogGraphTable: table.Graph)with
            {
                ObjectId = table.ObjectId
            };

            var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables.Select(selector: item => item.Id == tableId ? replacement : item), indexes: catalog.Definition.Indexes, scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections, assemblies: catalog.Definition.Assemblies);

            var written = await WriteDefinitionAsync(candidate: candidate, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await FlushCatalogStorageAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            catalog.Definition = candidate;
            catalog.RootPageId = written.RootPageId;

            if (generations is not null)
            {
                await generations.PublishStorageGenerationContextCatalogRootAsync(storageGenerationContext: new StorageGenerationContext { RootPageId = written.RootPageId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            return allocated;
        }
        finally
        {
            ExitCatalogMutation();
        }
    }

    private bool TryOpenTable(string name, out CatalogTable? table)
    {
        if (name is null)
        {
            throw new ArgumentNullException(paramName: nameof(name));
        }

        var matches = catalog.Definition.Tables.Where(predicate: candidate => StringComparer.Ordinal.Equals(x: candidate.Name, y: name))
            .Take(count: 2)
            .ToArray();

        table = matches.Length == 1 ? matches[0] : null;
        return table is not null;
    }

    private bool TryOpenTable(CatalogTableName name, out CatalogTable? table)
    {
        name = PrepareCatalogTableName(catalogTableName: name);
        table = catalog.Definition.Tables.SingleOrDefault(predicate: candidate => candidate.QualifiedName == name);
        return table is not null;
    }

    private bool TryOpenTable(TableId tableId, out CatalogTable? table)
    {
        table = catalog.Definition.Tables.SingleOrDefault(predicate: candidate => candidate.Id == tableId);
        return table is not null;
    }

    private ValueTask<CatalogTable> RegisterGraphNodeTableAsync(TableId tableId, ColumnId identityColumnId, IndexId identityIndexId, CancellationToken cancellationToken = default) =>
        RegisterGraphTableAsync(tableId: tableId, graph: CreateCatalogGraphTableNode(identityColumnId: (identityColumnId).Value, identityIndexId: (identityIndexId).Value), cancellationToken: cancellationToken);

    private ValueTask<CatalogTable> RegisterGraphEdgeTableAsync(TableId tableId, ColumnId identityColumnId, IndexId identityIndexId, TableId fromNodeTableId, ColumnId fromNodeColumnId, IndexId outgoingIndexId, TableId toNodeTableId, ColumnId toNodeColumnId, IndexId incomingIndexId, CancellationToken cancellationToken = default) =>
        RegisterGraphTableAsync(tableId: tableId, graph: CreateCatalogGraphTableEdge(identityColumnId: (identityColumnId).Value, identityIndexId: (identityIndexId).Value, fromNodeTableId: (fromNodeTableId).Value, fromNodeColumnId: (fromNodeColumnId).Value, outgoingIndexId: (outgoingIndexId).Value, toNodeTableId: (toNodeTableId).Value, toNodeColumnId: (toNodeColumnId).Value, incomingIndexId: (incomingIndexId).Value), cancellationToken: cancellationToken);

    private async ValueTask<CatalogTable> RegisterGraphTableAsync(TableId tableId, CatalogGraphTable graph, CancellationToken cancellationToken)
    {
        await EnterCatalogMutationAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            if (!TryOpenTable(tableId: tableId, table: out var table))
            {
                throw new ArgumentException(message: "Unknown table ID.", paramName: nameof(tableId));
            }

            if (table!.Graph is not null)
            {
                throw new CatalogConflictException(message: $"Table {FormatCatalogTableName(catalogTableName: table.QualifiedName)} is already registered for graph storage.");
            }

            var replacement = CreateCatalogTable(tableId: table.Id, name: table.Name, schemaVersion: checked(table.SchemaVersion + 1), pageId: table.FirstHeapPageId, columns: table.Columns, checkConstraints: table.CheckConstraints, nextIdentityValue: table.NextIdentityValue, databaseName: table.DatabaseName, schemaName: table.SchemaName, catalogSystemVersioning: table.SystemVersioning, catalogGraphTable: graph)with
            {
                ObjectId = table.ObjectId
            };

            var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables.Select(selector: item => item.Id == tableId ? replacement : item), indexes: catalog.Definition.Indexes, scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections, assemblies: catalog.Definition.Assemblies);

            await PublishDefinitionAsync(candidate: candidate, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return replacement;
        }
        finally
        {
            ExitCatalogMutation();
        }
    }

    private ValueTask<sql_storage_engine.Models.Heap.TableHeapBinding> OpenHeapAsync(CatalogTable table, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(argument: table);

        if (!catalog.Definition.Tables.Any(predicate: candidate => candidate.Id == table.Id))
        {
            throw new ArgumentException(message: "Table does not belong to this catalog.", paramName: nameof(table));
        }

        return OpenCatalogHeapAsync(table: table, cancellationToken: cancellationToken);
    }

    private ValueTask<CatalogIndex> CreateCatalogIndexAsync(string name, TableId tableId, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CancellationToken cancellationToken = default) =>
        CreateCatalogIndexCoreAsync(name: name, tableId: tableId, isUnique: isUnique, columns: columns, specializedOptions: null, btreeOptions: null, cancellationToken: cancellationToken);

    private ValueTask<CatalogIndex> CreateCatalogIndexAsync(string name, TableId tableId, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CatalogBTreeIndexOptions options, CancellationToken cancellationToken = default) =>
        CreateCatalogIndexCoreAsync(name: name, tableId: tableId, isUnique: isUnique, columns: columns, specializedOptions: null, btreeOptions: options, cancellationToken: cancellationToken);

    private ValueTask<CatalogIndex> CreateSpecializedCatalogIndexAsync(string name, TableId tableId, CatalogIndexedColumn column, CatalogSpecializedIndexOptions options, CancellationToken cancellationToken = default) =>
        CreateCatalogIndexCoreAsync(name: name, tableId: tableId, isUnique: false, columns: [column], specializedOptions: options, btreeOptions: null, cancellationToken: cancellationToken);

    private async ValueTask<CatalogIndex> CreateCatalogIndexCoreAsync(string name, TableId tableId, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CatalogSpecializedIndexOptions? specializedOptions, CatalogBTreeIndexOptions? btreeOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: columns);

        if (!TryOpenTable(tableId: tableId, table: out var table))
        {
            throw new ArgumentException(message: "Unknown table ID.", paramName: nameof(tableId));
        }

        if (catalog.Definition.Indexes.Any(predicate: index => index.TableId == tableId && StringComparer.Ordinal.Equals(x: index.Name, y: name)))
        {
            throw new CatalogConflictException(message: $"An index named '{name}' already exists on the table.");
        }

        var columnSnapshot = columns.ToArray();
        btreeOptions ??= CreateCatalogBTreeIndexOptions();

        var nextId = new sql_storage_engine.Models.Identifiers.IndexId
        {
            Value = catalog.Definition.Indexes.Count == 0 ? 1UL : checked(catalog.Definition.Indexes.Max(selector: index => index.Id.Value) + 1)
        };

        _ = CreateCatalogDefinition(tables: catalog.Definition.Tables, indexes: catalog.Definition.Indexes.Append(element: CreateCatalogIndex(indexId: nextId, name: name, tableId: tableId, pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, isUnique: isUnique, columns: columnSnapshot, catalogSpecializedIndexOptions: specializedOptions, storageKind: btreeOptions.StorageKind, isPrimaryKey: btreeOptions.IsPrimaryKey, includedColumns: btreeOptions.IncludedColumns, ignoreDuplicateKey: btreeOptions.IgnoreDuplicateKey)), scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections, assemblies: catalog.Definition.Assemblies);
        var allocatedPages = new List<ulong>();

        var allocations = new IndexAllocationContext
        {
            Tracking = true,
            Pages = allocatedPages
        };

        var publishedBuild = false;

        try
        {
            var rootContext = new IndexRootContext();

            var treeStorage = await catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 4, Id = nextId.Value, RootContext = rootContext, IsUnique = isUnique, Allocations = allocations }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            var root = treeStorage.RootPageId;
            var tree = treeStorage.TreeBinding!;

            var heap = await OpenHeapAsync(table: table!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            var rowTable = ToRowTable(table: table!);
            var vectorValueCount = 0;

            var vectorColumnPosition = specializedOptions?.Method == CatalogIndexMethod.Vector ? table!.Columns.Select(selector: (column, position) => (column, position))
            .Single(predicate: item => item.column.Id == columnSnapshot.Single()
            .ColumnId)
            .position : -1;

            await foreach (var entry in ScanCatalogHeap(heapBinding: heap, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
            {
                var row = (await rowCodec.ProcessRowEncodingContextDecodeAsync(rowEncodingContext: new RowEncodingContext { Source = entry.Row, Table = rowTable }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Row!;

                if (vectorColumnPosition >= 0 && !row.Values[vectorColumnPosition].IsNull)
                {
                    vectorValueCount++;
                }

                var provisional = CreateCatalogIndex(indexId: nextId, name: name, tableId: tableId, pageId: new PageId { Value = rootContext.RootPageId }, isUnique: isUnique, columns: columnSnapshot, catalogSpecializedIndexOptions: specializedOptions, storageKind: btreeOptions.StorageKind, isPrimaryKey: btreeOptions.IsPrimaryKey, includedColumns: btreeOptions.IncludedColumns, ignoreDuplicateKey: btreeOptions.IgnoreDuplicateKey);

                foreach (sql_storage_engine.Models.Indexes.IndexKey key in EncodeIndexKeys(row: row, catalogTable: table!, catalogIndex: provisional))
                {
                    await InsertCatalogTreeAsync(treeBinding: tree, indexKey: key, rowId: entry.RowId, includedPayload: EncodeIndexIncludedValues(row: row, catalogTable: table!, catalogIndex: provisional), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
            }

            if (vectorColumnPosition >= 0 && vectorValueCount < 100)
            {
                throw new InvalidOperationException(message: "A vector index requires at least 100 non-NULL vectors when it is created.");
            }

            var published = CreateCatalogIndex(indexId: nextId, name: name, tableId: tableId, pageId: new PageId { Value = rootContext.RootPageId }, isUnique: isUnique, columns: columnSnapshot, catalogSpecializedIndexOptions: specializedOptions, storageKind: btreeOptions.StorageKind, isPrimaryKey: btreeOptions.IsPrimaryKey, includedColumns: btreeOptions.IncludedColumns, ignoreDuplicateKey: btreeOptions.IgnoreDuplicateKey);
            var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables, indexes: catalog.Definition.Indexes.Append(element: published), scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections, assemblies: catalog.Definition.Assemblies);

            var written = await WriteDefinitionAsync(candidate: candidate, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await FlushCatalogStorageAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            catalog.Definition = candidate;
            catalog.RootPageId = written.RootPageId;
            catalog.IndexRoots![published.Id] = rootContext;
            catalog.IndexBindings![published.Id] = tree;
            RefreshCatalogTableStorageContexts();
            publishedBuild = true;
            return published;
        }
        catch (Exception exception)when (exception is not OperationCanceledException)
        {
            var unreclaimed = new List<PageId>();

            foreach (var pageId in allocatedPages.AsEnumerable()
            .Reverse()
            .Select(selector: value => new PageId { Value = value }))
            {
                try
                {
                    await ReleaseCatalogPageAsync(pageId: pageId, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (StorageException)
                {
                    unreclaimed.Add(item: pageId);
                }
            }

            throw new IndexBuildException(message: "Secondary-index build failed before publication.", allocatedPageIds: allocatedPages.Select(selector: value => new PageId { Value = value })
            .ToArray(), unreclaimedPageIds: unreclaimed, innerException: exception);
        }
        finally
        {
            allocations.Tracking = false;

            if (!publishedBuild)
            {
                await catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 15, Id = nextId.Value }, cancellationToken: CancellationToken.None)
            .ConfigureAwait(continueOnCapturedContext: false);
            }
        }
    }

    private bool TryOpenIndex(string name, TableId tableId, out CatalogIndex? index)
    {
        if (name is null)
        {
            throw new ArgumentNullException(paramName: nameof(name));
        }

        index = catalog.Definition.Indexes.SingleOrDefault(predicate: candidate => candidate.TableId == tableId && StringComparer.Ordinal.Equals(x: candidate.Name, y: name));
        return index is not null;
    }

    private async ValueTask<CatalogScalarType> CreateCatalogScalarTypeAsync(SqlType definition, CancellationToken cancellationToken = default)
    {
        var type = CreateCatalogScalarType(sqlType: definition);
        EnsureTypeNameAvailable(schemaName: type.Definition.UserTypeSchema!, name: type.Definition.UserTypeName!);
        var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables, indexes: catalog.Definition.Indexes, scalarTypes: catalog.Definition.ScalarTypes.Append(element: type), tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections, assemblies: catalog.Definition.Assemblies);

        await PublishDefinitionAsync(candidate: candidate, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return type;
    }

    private ValueTask<CatalogTableType> CreateCatalogTableTypeAsync(string schemaName, string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes = null, CancellationToken cancellationToken = default) =>
        CreateCatalogTableTypeAsync(schemaName: schemaName, name: name, columns: columns, indexes: indexes, checkConstraints: null, isMemoryOptimized: false, cancellationToken: cancellationToken);

    private async ValueTask<CatalogTableType> CreateCatalogTableTypeAsync(string schemaName, string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes, IEnumerable<CatalogCheckConstraint>? checkConstraints, bool isMemoryOptimized, CancellationToken cancellationToken = default)
    {
        var type = CreateCatalogTableType(schemaName: schemaName, name: name, columns: columns, indexes: indexes, checkConstraints: checkConstraints, isMemoryOptimized: isMemoryOptimized);
        EnsureTypeNameAvailable(schemaName: type.SchemaName, name: type.Name);
        var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables, indexes: catalog.Definition.Indexes, scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes.Append(element: type), xmlSchemaCollections: catalog.Definition.XmlSchemaCollections, assemblies: catalog.Definition.Assemblies);

        await PublishDefinitionAsync(candidate: candidate, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return type;
    }

    private bool TryOpenScalarType(string schemaName, string name, out CatalogScalarType? type)
    {
        type = catalog.Definition.ScalarTypes.SingleOrDefault(predicate: candidate => StringComparer.Ordinal.Equals(x: candidate.Definition.UserTypeSchema!, y: schemaName) && StringComparer.Ordinal.Equals(x: candidate.Definition.UserTypeName!, y: name));
        return type is not null;
    }

    private bool TryOpenTableType(string schemaName, string name, out CatalogTableType? type)
    {
        type = catalog.Definition.TableTypes.SingleOrDefault(predicate: candidate => StringComparer.Ordinal.Equals(x: candidate.SchemaName, y: schemaName) && StringComparer.Ordinal.Equals(x: candidate.Name, y: name));
        return type is not null;
    }

    private bool TryOpenXmlSchemaCollection(string schemaName, string name, out SqlXmlSchemaCollection? collection)
    {
        collection = catalog.Definition.XmlSchemaCollections.SingleOrDefault(predicate: candidate => StringComparer.Ordinal.Equals(x: candidate.SchemaName, y: schemaName) && StringComparer.Ordinal.Equals(x: candidate.Name, y: name));
        return collection is not null;
    }

    private bool TryOpenAssembly(string name, out CatalogAssembly? assembly)
    {
        assembly = catalog.Definition.Assemblies.SingleOrDefault(predicate: candidate => StringComparer.Ordinal.Equals(x: candidate.Name, y: name));
        return assembly is not null;
    }

    private async ValueTask<SqlXmlSchemaCollection> CreateSqlXmlSchemaCollectionAsync(SqlXmlSchemaCollection collection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(argument: collection);

        if (TryOpenXmlSchemaCollection(schemaName: collection.SchemaName, name: collection.Name, collection: out _))
        {
            throw new CatalogConflictException(message: $"XML schema collection {ReadSqlXmlSchemaCollectionQualifiedName(sqlXmlSchemaCollection: collection)} already exists.");
        }

        var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables, indexes: catalog.Definition.Indexes, scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections.Append(element: collection), assemblies: catalog.Definition.Assemblies);

        await PublishDefinitionAsync(candidate: candidate, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return collection;
    }

    private async ValueTask<SqlXmlSchemaCollection> AlterXmlSchemaCollectionAsync(string schemaName, string name, IEnumerable<string> additionalDefinitions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(argument: additionalDefinitions);

        if (!TryOpenXmlSchemaCollection(schemaName: schemaName, name: name, collection: out var existing))
        {
            throw new KeyNotFoundException(message: $"Unknown XML schema collection [{schemaName}].[{name}].");
        }

        var updated = CreateSqlXmlSchemaCollection(schemaName: schemaName, name: name, definitions: ReadSqlXmlSchemaCollectionDefinitions(sqlXmlSchemaCollection: existing!)
            .Concat(second: additionalDefinitions));

        var candidate = RebindXmlCollection(existing: existing!, updated: updated);

        await PublishDefinitionAsync(candidate: candidate, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return updated;
    }

    private async ValueTask DropXmlSchemaCollectionAsync(string schemaName, string name, CancellationToken cancellationToken = default)
    {
        if (!TryOpenXmlSchemaCollection(schemaName: schemaName, name: name, collection: out var existing))
        {
            throw new KeyNotFoundException(message: $"Unknown XML schema collection [{schemaName}].[{name}].");
        }

        if (AllColumns(definition: catalog.Definition)
            .Any(predicate: column => column.Type.XmlSchemaCollection?.Equals(other: existing) == true))
        {
            throw new CatalogConflictException(message: $"XML schema collection {ReadSqlXmlSchemaCollectionQualifiedName(sqlXmlSchemaCollection: existing!)} is referenced by a column.");
        }

        var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables, indexes: catalog.Definition.Indexes, scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections.Where(predicate: item => !item.Equals(other: existing)), assemblies: catalog.Definition.Assemblies);

        await PublishDefinitionAsync(candidate: candidate, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async ValueTask<CatalogAssembly> CreateCatalogAssemblyAsync(CatalogAssembly assembly, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(argument: assembly);

        if (TryOpenAssembly(name: assembly.Name, assembly: out _))
        {
            throw new CatalogConflictException(message: $"Assembly '{assembly.Name}' already exists.");
        }

        var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables, indexes: catalog.Definition.Indexes, scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections, assemblies: catalog.Definition.Assemblies.Append(element: assembly));

        await PublishDefinitionAsync(candidate: candidate, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return assembly;
    }

    private async ValueTask DropAssemblyAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!TryOpenAssembly(name: name, assembly: out _))
        {
            throw new KeyNotFoundException(message: $"Unknown assembly '{name}'.");
        }

        if (catalog.Definition.ScalarTypes.Any(predicate: type => type.Definition.UserTypeKind == SqlUserTypeKind.Clr && StringComparer.Ordinal.Equals(x: type.Definition.ClrAssemblyName, y: name)))
        {
            throw new CatalogConflictException(message: $"Assembly '{name}' is referenced by a CLR type.");
        }

        var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables, indexes: catalog.Definition.Indexes, scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections, assemblies: catalog.Definition.Assemblies.Where(predicate: assembly => !StringComparer.Ordinal.Equals(x: assembly.Name, y: name)));

        await PublishDefinitionAsync(candidate: candidate, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private IndexTreeBinding OpenIndex(CatalogIndex index)
    {
        ArgumentNullException.ThrowIfNull(argument: index);

        if (!catalog.Definition.Indexes.Any(predicate: candidate => candidate.Id == index.Id))
        {
            throw new ArgumentException(message: "Index does not belong to this catalog.", paramName: nameof(index));
        }

        if (!catalog.IndexRoots!.TryGetValue(key: index.Id, value: out var root))
        {
            root = new IndexRootContext
            {
                RootPageId = index.RootPageId.Value
            };

            catalog.IndexRoots.Add(key: index.Id, value: root);
        }

        var binding = catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 6, Id = index.Id.Value, RootPageId = index.RootPageId, RootContext = root, IsUnique = index.IsUnique }, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .TreeBinding!;

        catalog.IndexBindings![index.Id] = binding;
        return binding;
    }

    private static TableDefinition ToRowTable(CatalogTable table) =>
        new TableDefinition
    {
        Columns = Array.AsReadOnly(array: table.Columns.Select(selector: column => new ColumnDefinition { Id = column.Id, Name = column.Name, Type = column.Type, IsNullable = column.IsNullable, Encryption = column.Encryption, IsSparse = column.IsSparse, IsColumnSet = column.IsColumnSet })
            .ToArray())
    };

    private void EnsureTypeNameAvailable(string schemaName, string name)
    {
        if (catalog.Definition.ScalarTypes.Any(predicate: type => StringComparer.Ordinal.Equals(x: type.Definition.UserTypeSchema!, y: schemaName) && StringComparer.Ordinal.Equals(x: type.Definition.UserTypeName!, y: name)) || catalog.Definition.TableTypes.Any(predicate: type => StringComparer.Ordinal.Equals(x: type.SchemaName, y: schemaName) && StringComparer.Ordinal.Equals(x: type.Name, y: name)))
        {
            throw new CatalogConflictException(message: $"A type named '[{schemaName}].[{name}]' already exists.");
        }
    }

    private async ValueTask PublishDefinitionAsync(CatalogDefinition candidate, CancellationToken cancellationToken)
    {
        var written = await WriteDefinitionAsync(candidate: candidate, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await FlushCatalogStorageAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        catalog.Definition = candidate;
        catalog.RootPageId = written.RootPageId;
        RefreshCatalogTableStorageContexts();
    }

    private CatalogDefinition RebindXmlCollection(SqlXmlSchemaCollection existing, SqlXmlSchemaCollection updated)
    {
        CatalogColumn RebindColumn(CatalogColumn column)
        {
            var type = column.Type.XmlSchemaCollection?.Equals(other: existing) == true ? column.Type with
            {
                XmlSchemaCollection = updated
            }

            : column.Type;

            return CreateCatalogColumn(columnId: column.Id, name: column.Name, sqlType: type, isNullable: column.IsNullable, defaultExpression: column.DefaultExpression, catalogIdentity: column.Identity, isRowGuidCol: column.IsRowGuidCol, computedExpression: column.ComputedExpression, isComputedPersisted: column.IsComputedPersisted, isSparse: column.IsSparse, isColumnSet: column.IsColumnSet, isFileStream: column.IsFileStream, maskingFunction: column.MaskingFunction, catalogColumnEncryption: column.Encryption, generatedAlways: column.GeneratedAlways, isHidden: column.IsHidden);
        }

        var tables = catalog.Definition.Tables.Select(selector: table => CreateCatalogTable(tableId: table.Id, name: table.Name, schemaVersion: table.SchemaVersion, pageId: table.FirstHeapPageId, columns: table.Columns.Select(selector: RebindColumn), checkConstraints: table.CheckConstraints, nextIdentityValue: table.NextIdentityValue, databaseName: table.DatabaseName, schemaName: table.SchemaName, catalogSystemVersioning: table.SystemVersioning, catalogGraphTable: table.Graph)with { ObjectId = table.ObjectId });
        var tableTypes = catalog.Definition.TableTypes.Select(selector: type => CreateCatalogTableType(schemaName: type.SchemaName, name: type.Name, columns: type.Columns.Select(selector: RebindColumn), indexes: type.Indexes, checkConstraints: type.CheckConstraints, isMemoryOptimized: type.IsMemoryOptimized));
        return CreateCatalogDefinition(tables: tables, indexes: catalog.Definition.Indexes, scalarTypes: catalog.Definition.ScalarTypes, tableTypes: tableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections.Select(selector: item => item.Equals(other: existing) ? updated : item), assemblies: catalog.Definition.Assemblies);
    }

    private static IEnumerable<CatalogColumn> AllColumns(CatalogDefinition definition) =>
        definition.Tables.SelectMany(selector: table => table.Columns)
            .Concat(second: definition.TableTypes.SelectMany(selector: type => type.Columns));

    private ValueTask<(PageId RootPageId, IReadOnlyList<PageId> PageIds)> WriteDefinitionAsync(CatalogDefinition candidate, CancellationToken token)
    {
        candidate.Security = catalog.Definition.Security;
        return WriteCatalogPayloadAsync(catalogPayload: EncodeCatalogPayload(catalogDefinition: candidate), cancellationToken: token);
    }

    private async ValueTask WriteSecurityAsync(StorageSecurityState state, CancellationToken token)
    {
        await EnterCatalogMutationAsync(cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables, indexes: catalog.Definition.Indexes, scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections, assemblies: catalog.Definition.Assemblies)with
            {
                Security = state
            };

            var written = await WriteCatalogPayloadAsync(catalogPayload: EncodeCatalogPayload(catalogDefinition: candidate), cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);

            await FlushCatalogStorageAsync(cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);

            catalog.Definition = candidate;
            catalog.RootPageId = written.RootPageId;
        }
        finally
        {
            ExitCatalogMutation();
        }
    }

    private SqlXmlSchemaCollection CreateSqlXmlSchemaCollection(string schemaName, string name, IEnumerable<string> definitions) =>
        catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 3, SqlXmlSchemaCollection = new SqlXmlSchemaCollection { SchemaName = schemaName, Name = name, DefinitionSource = definitions } })
            .PrepareSqlXmlSchemaCollectionResult!;

    private IReadOnlyList<string> ReadSqlXmlSchemaCollectionDefinitions(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 4, SqlXmlSchemaCollection = sqlXmlSchemaCollection })
            .ReadSqlXmlSchemaCollectionDefinitionsResult!;

    private CatalogSystemVersioning CreateCatalogSystemVersioning(TableId tableId, ulong periodStartColumnId, ulong periodEndColumnId) =>
        catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 10, CatalogSystemVersioning = new CatalogSystemVersioning { HistoryTableId = tableId, PeriodStartColumnId = new ColumnId { Value = periodStartColumnId }, PeriodEndColumnId = new ColumnId { Value = periodEndColumnId } } })
            .PrepareCatalogSystemVersioningResult!;

    private CatalogGraphTable CreateCatalogGraphTableNode(ulong identityColumnId, ulong identityIndexId) =>
        catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 11, CatalogGraphTable = new CatalogGraphTable { Kind = GraphTableKind.Node, IdentityColumnId = new sql_storage_engine.Models.Identifiers.ColumnId { Value = identityColumnId }, IdentityIndexId = new IndexId { Value = identityIndexId } } })
            .PrepareCatalogGraphTableResult!;

    private CatalogGraphTable CreateCatalogGraphTableEdge(ulong identityColumnId, ulong identityIndexId, ulong fromNodeTableId, ulong fromNodeColumnId, ulong outgoingIndexId, ulong toNodeTableId, ulong toNodeColumnId, ulong incomingIndexId) =>
        catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 11, CatalogGraphTable = new CatalogGraphTable { Kind = GraphTableKind.Edge, IdentityColumnId = new sql_storage_engine.Models.Identifiers.ColumnId { Value = identityColumnId }, IdentityIndexId = new IndexId { Value = identityIndexId }, FromNodeTableId = new TableId { Value = fromNodeTableId }, FromNodeColumnId = new sql_storage_engine.Models.Identifiers.ColumnId { Value = fromNodeColumnId }, OutgoingIndexId = new IndexId { Value = outgoingIndexId }, ToNodeTableId = new TableId { Value = toNodeTableId }, ToNodeColumnId = new sql_storage_engine.Models.Identifiers.ColumnId { Value = toNodeColumnId }, IncomingIndexId = new IndexId { Value = incomingIndexId } } })
            .PrepareCatalogGraphTableResult!;

    private CatalogTable CreateCatalogTable(TableId tableId, string name, ulong schemaVersion, PageId pageId, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, BigInteger? nextIdentityValue = null, string databaseName = "default", string schemaName = "dbo", CatalogSystemVersioning? catalogSystemVersioning = null, CatalogGraphTable? catalogGraphTable = null) =>
        catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 12, CatalogTable = new CatalogTable { IdSource = tableId, NameSource = name, SchemaVersionSource = schemaVersion, FirstHeapPageIdSource = pageId, ColumnsSource = columns, CheckConstraintsSource = checkConstraints, NextIdentityValueSource = nextIdentityValue, DatabaseNameSource = databaseName, SchemaNameSource = schemaName, SystemVersioningSource = catalogSystemVersioning, GraphSource = catalogGraphTable } })
            .PrepareCatalogTableResult!;

    private CatalogIndex CreateCatalogIndex(IndexId indexId, string name, TableId tableId, PageId pageId, bool isUnique, IEnumerable<CatalogIndexedColumn> columns, CatalogSpecializedIndexOptions? catalogSpecializedIndexOptions = null, CatalogIndexStorageKind storageKind = CatalogIndexStorageKind.NonClustered, bool isPrimaryKey = false, IEnumerable<ColumnId>? includedColumns = null, bool ignoreDuplicateKey = false) =>
        PrepareCatalogIndex(catalogIndex: new CatalogIndex { Id = indexId, Name = name, TableId = tableId, RootPageId = pageId, IsUnique = isUnique, ColumnSource = columns, SpecializedOptions = catalogSpecializedIndexOptions, StorageKind = storageKind, IsPrimaryKey = isPrimaryKey, IncludedColumnSource = includedColumns, IgnoreDuplicateKey = ignoreDuplicateKey });

    private sql_storage_engine.Models.Catalog.CatalogScalarType CreateCatalogScalarType(SqlType sqlType) =>
        catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 15, SqlTypeContext = new SqlTypeContext { SqlTypeSource = sqlType } })
            .CreateSqlTypeContextCatalogScalarTypeResult!.CatalogScalarTypeResult!;

    private CatalogTableType CreateCatalogTableType(string schemaName, string name, IEnumerable<CatalogColumn> columns, IEnumerable<CatalogTableTypeIndex>? indexes = null, IEnumerable<CatalogCheckConstraint>? checkConstraints = null, bool isMemoryOptimized = false) =>
        catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 17, CatalogTableType = new CatalogTableType { SchemaNameSource = schemaName, NameSource = name, ColumnsSource = columns, IndexesSource = indexes, CheckConstraintsSource = checkConstraints, IsMemoryOptimizedSource = isMemoryOptimized } })
            .PrepareCatalogTableTypeResult!;

    private CatalogDefinition CreateCatalogDefinition(IEnumerable<CatalogTable> tables, IEnumerable<CatalogIndex> indexes, IEnumerable<CatalogScalarType>? scalarTypes = null, IEnumerable<CatalogTableType>? tableTypes = null, IEnumerable<SqlXmlSchemaCollection>? xmlSchemaCollections = null, IEnumerable<CatalogAssembly>? assemblies = null) =>
        catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 18, CatalogDefinition = new CatalogDefinition { TablesSource = tables, IndexesSource = indexes, ScalarTypesSource = scalarTypes, TableTypesSource = tableTypes, XmlSchemaCollectionsSource = xmlSchemaCollections, AssembliesSource = assemblies } })
            .PrepareCatalogDefinitionResult!;

    private CatalogColumn CreateCatalogColumn(ColumnId columnId, string name, SqlType sqlType, bool isNullable, string? defaultExpression = null, CatalogIdentity? catalogIdentity = null, bool isRowGuidCol = false, string? computedExpression = null, bool isComputedPersisted = false, bool isSparse = false, bool isColumnSet = false, bool isFileStream = false, string? maskingFunction = null, CatalogColumnEncryption? catalogColumnEncryption = null, CatalogGeneratedAlwaysKind generatedAlways = CatalogGeneratedAlwaysKind.None, bool isHidden = false) =>
        catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 21, CatalogColumn = new CatalogColumn { IdSource = columnId, NameSource = name, TypeSource = sqlType, IsNullableSource = isNullable, DefaultExpressionSource = defaultExpression, IdentitySource = catalogIdentity, IsRowGuidColSource = isRowGuidCol, ComputedExpressionSource = computedExpression, IsComputedPersistedSource = isComputedPersisted, IsSparseSource = isSparse, IsColumnSetSource = isColumnSet, IsFileStreamSource = isFileStream, MaskingFunctionSource = maskingFunction, EncryptionSource = catalogColumnEncryption, GeneratedAlwaysSource = generatedAlways, IsHiddenSource = isHidden } })
            .PrepareCatalogColumnResult!;

    private CatalogIndex PrepareCatalogIndex(CatalogIndex catalogIndex) =>
        catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 24, CatalogIndex = catalogIndex })
            .PrepareCatalogIndexResult!;
}