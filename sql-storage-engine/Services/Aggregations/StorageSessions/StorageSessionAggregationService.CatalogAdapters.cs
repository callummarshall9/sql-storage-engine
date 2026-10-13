// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using sql_storage_engine.Catalog;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private byte[] EncodeCatalogPayload(CatalogDefinition catalogDefinition) =>
        catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 27, CatalogDefinition = catalogDefinition })
            .Payload!;

    private async ValueTask<(PageId RootPageId, IReadOnlyList<PageId> PageIds)> WriteCatalogPayloadAsync(byte[] catalogPayload, CancellationToken cancellationToken)
    {
        var written = await catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 8, Payload = catalogPayload }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return (written.RootPageId, written.PageIds!);
    }

    private ValueTask FlushCatalogStorageAsync(CancellationToken cancellationToken) =>
        new ValueTask(task: catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 10 }, cancellationToken: cancellationToken)
            .AsTask());

    private ValueTask ReleaseCatalogPageAsync(PageId pageId, CancellationToken cancellationToken) =>
        new ValueTask(task: catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 11, RootPageId = pageId }, cancellationToken: cancellationToken)
            .AsTask());

    private ValueTask EnterCatalogMutationAsync(CancellationToken cancellationToken) =>
        new ValueTask(task: catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 12 }, cancellationToken: cancellationToken)
            .AsTask());

    private void ExitCatalogMutation() =>
        catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 13 }, cancellationToken: CancellationToken.None)
            .GetAwaiter()
            .GetResult();

    private async ValueTask<TableHeapBinding> CreateCatalogTableHeapBindingAsync(TableId tableId, CancellationToken cancellationToken)
    {
        var heap = (await catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 1, Id = tableId.Value }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).HeapBinding!;

        catalog.HeapBindings![tableId] = heap;
        return heap;
    }

    private async ValueTask<TableHeapBinding> OpenCatalogHeapAsync(CatalogTable table, CancellationToken cancellationToken)
    {
        var heap = (await catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 2, Id = table.Id.Value, RootPageId = table.FirstHeapPageId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).HeapBinding!;

        catalog.HeapBindings![table.Id] = heap;
        return heap;
    }

    private async IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)> ScanCatalogHeap(TableHeapBinding heapBinding, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rows = (await catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 3, HeapBinding = heapBinding }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Rows!;

        await foreach (var row in rows.ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return row;
        }
    }

    private ValueTask InsertCatalogTreeAsync(IndexTreeBinding treeBinding, IndexKey indexKey, RowId rowId, ReadOnlyMemory<byte> includedPayload, CancellationToken cancellationToken) =>
        new ValueTask(task: catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 5, TreeBinding = treeBinding, Tree = new IndexTreeContext { Key = indexKey, RowId = rowId, IncludedPayload = includedPayload } }, cancellationToken: cancellationToken)
            .AsTask());

    private IReadOnlyList<IndexKey> EncodeIndexKeys(Row row, CatalogTable catalogTable, CatalogIndex catalogIndex) =>
        catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 1, Row = row, Table = catalogTable, Index = catalogIndex })
            .Entries!;

    private byte[] EncodeIndexIncludedValues(Row row, CatalogTable catalogTable, CatalogIndex catalogIndex) =>
        catalogIndexKeys.PrepareCatalogIndexKeyRequest(catalogIndexKeyRequest: new CatalogIndexKeyRequest { Operation = 8, Row = row, Table = catalogTable, Index = catalogIndex })
            .PayloadResult!;

    private static CatalogTableName CreateCatalogTableName(string databaseName, string schemaName, string name) =>
        PrepareCatalogTableName(catalogTableName: new CatalogTableName { DatabaseName = databaseName, SchemaName = schemaName, Name = name });

    private static CatalogTableName PrepareCatalogTableName(CatalogTableName catalogTableName)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogTableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: catalogTableName.DatabaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: catalogTableName.SchemaName);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: catalogTableName.Name);
        return catalogTableName;
    }

    private static string FormatCatalogTableName(CatalogTableName catalogTableName) =>
        $"[{catalogTableName.DatabaseName}].[{catalogTableName.SchemaName}].[{catalogTableName.Name}]";

    private static string ReadSqlXmlSchemaCollectionQualifiedName(SqlXmlSchemaCollection sqlXmlSchemaCollection) =>
        $"[{sqlXmlSchemaCollection.SchemaName}].[{sqlXmlSchemaCollection.Name}]";

    private static CatalogBTreeIndexOptions CreateCatalogBTreeIndexOptions() =>
        new CatalogBTreeIndexOptions
    {
        StorageKind = CatalogIndexStorageKind.NonClustered,
        IncludedColumns = Array.Empty<ColumnId>()
    };

    private SqlValue CreateAllocatedIdentitySqlValue(CatalogColumn column, BigInteger current)
    {
        var type = column.Type;

        while (type.BaseType is not null)
        {
            type = type.BaseType;
        }

        SqlValue value = type.Name is SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or SqlTypeName.BigInt ? new IntegerSqlValue
        {
            Value = checked((long)current),
            StorageFamily = SqlStorageFamily.Integer
        }

        : new DecimalSqlValue
        {
            Value = new SqlDecimal
            {
                Coefficient = current,
                Scale = 0
            },
            StorageFamily = SqlStorageFamily.Decimal
        };

        return rowCodec.ProcessRowEncodingContextLogicalEncode(rowEncodingContext: new RowEncodingContext { Value = value, Column = new ColumnDefinition { Id = column.Id, Name = column.Name, Type = column.Type, IsNullable = false } })
            .Value!;
    }

    private async ValueTask PublishCatalogIndexRootsAsync(CancellationToken cancellationToken)
    {
        var changed = new Dictionary<IndexId, CatalogIndex>();

        foreach (var index in catalog.Definition.Indexes)
        {
            if (catalog.IndexRoots!.TryGetValue(key: index.Id, value: out var root) && root.RootPageId != index.RootPageId.Value)
            {
                changed.Add(key: index.Id, value: CreateCatalogIndex(indexId: index.Id, name: index.Name, tableId: index.TableId, pageId: new PageId { Value = root.RootPageId }, isUnique: index.IsUnique, columns: index.Columns, catalogSpecializedIndexOptions: index.SpecializedOptions, storageKind: index.StorageKind, isPrimaryKey: index.IsPrimaryKey, includedColumns: index.IncludedColumns, ignoreDuplicateKey: index.IgnoreDuplicateKey));
            }
        }

        if (changed.Count == 0)
        {
            return;
        }

        await FlushCatalogStorageAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var candidate = CreateCatalogDefinition(tables: catalog.Definition.Tables, indexes: catalog.Definition.Indexes.Select(selector: index => changed.GetValueOrDefault(key: index.Id, defaultValue: index)), scalarTypes: catalog.Definition.ScalarTypes, tableTypes: catalog.Definition.TableTypes, xmlSchemaCollections: catalog.Definition.XmlSchemaCollections, assemblies: catalog.Definition.Assemblies);

        await PublishDefinitionAsync(candidate: candidate, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    internal static StorageSessionContext CreateStandaloneStorageSessionContext(CatalogTable catalogTable, TableHeapBinding tableHeapBinding, RowCodecBinding rowCodecBinding, TableIndexBinding[] tableIndexBindings)
    {
        var service = CreateStorageSessionAggregationService(configuration: new StorageSessionConfiguration { RowCodecBinding = rowCodecBinding });
        service.catalog.Definition = service.CreateCatalogDefinition(tables: [catalogTable], indexes: tableIndexBindings.Select(selector: index => index.Definition!));
        var binding = service.BindTableMutationContext(tableStorageContext: new sql_storage_engine.Models.Tables.TableStorageContext { Definition = catalogTable, Schema = ToRowTable(table: catalogTable), HeapBinding = tableHeapBinding, Indexes = tableIndexBindings });
        service.catalog.TableStorages![catalogTable.Id] = binding;

        return new StorageSessionContext
        {
            Binding = new StorageSessionBinding
            {
                Service = service
            },
            Table = binding
        };
    }

    private TableIndexBinding ReadCatalogIndexBinding(IndexId indexId) =>
        CreateCatalogTableIndexBinding(catalogIndex: catalog.Definition.Indexes.Single(predicate: index => index.Id == indexId));

    private async ValueTask<sql_storage_engine.Models.Tables.TableStorageContext> ReadCatalogTableStorageContextAsync(TableId tableId, CancellationToken cancellationToken)
    {
        if (catalog.TableStorages!.TryGetValue(key: tableId, value: out var storage))
        {
            return storage;
        }

        var table = catalog.Definition.Tables.Single(predicate: table => table.Id == tableId);

        var heap = await OpenHeapAsync(table: table, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var bindings = catalog.Definition.Indexes.Where(predicate: index => index.TableId == tableId)
            .Select(selector: index => CreateCatalogTableIndexBinding(catalogIndex: index))
            .ToArray();

        storage = BindTableMutationContext(tableStorageContext: new sql_storage_engine.Models.Tables.TableStorageContext { Definition = table, Schema = ToRowTable(table: table), HeapBinding = heap, Indexes = bindings });
        catalog.TableStorages[tableId] = storage;
        return storage;
    }

    private void RefreshCatalogTableStorageContexts()
    {
        foreach (var table in catalog.Definition.Tables)
        {
            if (catalog.TableStorages!.TryGetValue(key: table.Id, value: out var storage))
            {
                storage.Definition = table;
                storage.Schema = ToRowTable(table: table);

                storage.Indexes = catalog.Definition.Indexes.Where(predicate: index => index.TableId == table.Id)
            .Select(selector: index => CreateCatalogTableIndexBinding(catalogIndex: index))
            .ToArray();
            }
        }
    }
}