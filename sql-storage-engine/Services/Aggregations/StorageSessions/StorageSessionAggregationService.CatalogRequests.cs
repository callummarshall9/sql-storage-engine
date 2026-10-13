// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Tables;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    public ValueTask<StorageSessionContext> ExecuteStorageSessionContextCatalogAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCatalogSessionCall(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);

        return await ExecuteCatalogRequestAsync(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    private async ValueTask<StorageSessionContext> ExecuteCatalogRequestAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ValidateCatalogSessionCall(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var request = storageSessionContext.Catalog!;

        switch (request.Operation)
        {
            case 1:
                request.TableResult = await CreateCatalogTableAsync(name: request.Name!, schemaVersion: request.SchemaVersion!, columns: request.Columns!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 2:
                request.TableResult = await CreateCatalogTableAsync(name: request.Name!, schemaVersion: request.SchemaVersion!, columns: request.Columns!, checkConstraints: request.CheckConstraints, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 3:
                request.TableResult = await CreateCatalogTableAsync(name: request.QualifiedName!, schemaVersion: request.SchemaVersion!, columns: request.Columns!, checkConstraints: request.CheckConstraints, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 4:
                ValidateTemporalCatalogNames(request: request);
                request.TableResult = await CreateSystemVersionedCatalogTableAsync(name: request.QualifiedNames![0]!, historyName: request.QualifiedNames![1]!, schemaVersion: request.SchemaVersion!, columns: request.Columns!, periodStartColumnId: request.PeriodColumnIds![0], periodEndColumnId: request.PeriodColumnIds![1], checkConstraints: request.CheckConstraints, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 5:
                request.ValueResult = await AllocateIdentityAsync(tableId: request.TableId!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 6:
                request.Found = TryOpenTable(name: request.Name!, table: out var tableResult6);
                request.Table = tableResult6;
                break;
            case 7:
                request.Found = TryOpenTable(name: request.QualifiedName!, table: out var tableResult7);
                request.Table = tableResult7;
                break;
            case 8:
                request.Found = TryOpenTable(tableId: request.TableId!, table: out var tableResult8);
                request.Table = tableResult8;
                break;
            case 9:
                request.TableResult = await RegisterGraphNodeTableAsync(tableId: request.TableId!, identityColumnId: request.IdentityColumnId!, identityIndexId: request.IdentityIndexId!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 10:
                request.TableResult = await RegisterGraphEdgeTableAsync(tableId: request.TableId!, identityColumnId: request.IdentityColumnId!, identityIndexId: request.IdentityIndexId!, fromNodeTableId: request.FromNodeTableId!, fromNodeColumnId: request.FromNodeColumnId!, outgoingIndexId: request.OutgoingIndexId!, toNodeTableId: request.ToNodeTableId!, toNodeColumnId: request.ToNodeColumnId!, incomingIndexId: request.IncomingIndexId!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 11:
                request.HeapResult = await OpenHeapAsync(table: request.CatalogTableTable!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 12:
                request.IndexResult = await CreateCatalogIndexAsync(name: request.Name!, tableId: request.TableId!, isUnique: request.IsUnique!, columns: request.IndexedColumns!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 13:
                request.IndexResult = await CreateCatalogIndexAsync(name: request.Name!, tableId: request.TableId!, isUnique: request.IsUnique!, columns: request.IndexedColumns!, options: request.Options!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 14:
                request.IndexResult = await CreateSpecializedCatalogIndexAsync(name: request.Name!, tableId: request.TableId!, column: request.Column!, options: request.CatalogSpecializedIndexOptionsOptions!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 15:
                request.Found = TryOpenIndex(name: request.Name!, tableId: request.TableId!, index: out var indexResult15);
                request.Index = indexResult15;
                break;
            case 16:
                request.ScalarTypeResult = await CreateCatalogScalarTypeAsync(definition: request.TypeDefinition!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 17:
                request.TableTypeResult = await CreateCatalogTableTypeAsync(schemaName: request.SchemaName!, name: request.Name!, columns: request.Columns!, indexes: request.TableTypeIndexes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 18:
                request.TableTypeResult = await CreateCatalogTableTypeAsync(schemaName: request.SchemaName!, name: request.Name!, columns: request.Columns!, indexes: request.TableTypeIndexes, checkConstraints: request.CheckConstraints, isMemoryOptimized: request.IsMemoryOptimized!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 19:
                request.Found = TryOpenScalarType(schemaName: request.SchemaName!, name: request.Name!, type: out var typeResult19);
                request.Type = typeResult19;
                break;
            case 20:
                request.Found = TryOpenTableType(schemaName: request.SchemaName!, name: request.Name!, type: out var typeResult20);
                request.CatalogTableTypeType = typeResult20;
                break;
            case 21:
                request.Found = TryOpenXmlSchemaCollection(schemaName: request.SchemaName!, name: request.Name!, collection: out var collectionResult21);
                request.Collection = collectionResult21;
                break;
            case 22:
                request.Found = TryOpenAssembly(name: request.Name!, assembly: out var assemblyResult22);
                request.Assembly = assemblyResult22;
                break;
            case 23:
                request.XmlSchemaCollectionResult = await CreateSqlXmlSchemaCollectionAsync(collection: request.SqlXmlSchemaCollectionCollection!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 24:
                request.XmlSchemaCollectionResult = await AlterXmlSchemaCollectionAsync(schemaName: request.SchemaName!, name: request.Name!, additionalDefinitions: request.AdditionalDefinitions!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 25:
                await DropXmlSchemaCollectionAsync(schemaName: request.SchemaName!, name: request.Name!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 26:
                request.AssemblyResult = await CreateCatalogAssemblyAsync(assembly: request.CatalogAssemblyAssembly!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 27:
                await DropAssemblyAsync(name: request.Name!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 28:
                request.TreeResult = OpenIndex(index: request.CatalogIndexIndex!);
                break;
            case 29:
                await WriteSecurityAsync(state: request.State!, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 102:
                request.TableResult = await RegisterGraphTableAsync(tableId: request.TableId, graph: request.Graph!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 100:
                var source = await catalogStorage.ProcessCatalogStorageContextAsync(catalogStorageContext: new CatalogStorageContext { Operation = 9, RootPageId = request.RootPageId!.Value }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                catalog.Definition = catalogCodec.ProcessCatalogCodecContext(catalogCodecContext: new CatalogCodecContext { Operation = 28, Payload = source.Payload })
            .CatalogDefinition!;
                catalog.RootPageId = request.RootPageId;
                break;
            case 101:
                var table = request.Table!;
                var heap = await OpenHeapAsync(table: table, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                var bindings = catalog.Definition.Indexes.Where(predicate: index => index.TableId == table.Id)
            .Select(selector: index => CreateCatalogTableIndexBinding(catalogIndex: index))
            .ToArray();
                var tableBinding = BindTableMutationContext(tableStorageContext: new TableStorageContext { Definition = table, Schema = ToRowTable(table: table), HeapBinding = heap, Indexes = bindings });
                catalog.TableStorages![table.Id] = tableBinding;
                storageSessionContext.Table = tableBinding;
                break;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(request.Operation));
        }

        request.Definition = catalog.Definition;
        request.RootPageId = catalog.RootPageId;

        storageSessionContext.Binding = new StorageSessionBinding
        {
            Service = this
        };

        return storageSessionContext;
    }

    private TableIndexBinding CreateCatalogTableIndexBinding(CatalogIndex catalogIndex)
    {
        var binding = OpenIndex(index: catalogIndex);

        return new TableIndexBinding
        {
            Definition = catalogIndex,
            Service = binding.Service,
            Context = new IndexTreeContext
            {
                Definition = catalogIndex,
                RootPageId = catalogIndex.RootPageId
            }
        };
    }

    private static void ValidateCatalogSessionCall(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        ValidateStorageSessionContext(storageSessionContext: storageSessionContext);
        ArgumentNullException.ThrowIfNull(argument: storageSessionContext.Catalog);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateTemporalCatalogNames(CatalogSessionContext request)
    {
        ArgumentNullException.ThrowIfNull(argument: request.QualifiedNames);
        ArgumentNullException.ThrowIfNull(argument: request.PeriodColumnIds);

        if (request.QualifiedNames.Length != 2 || request.PeriodColumnIds.Length != 2)
        {
            throw new ArgumentException(message: "Temporal catalog registration requires two table names and two period column IDs.");
        }

        ArgumentNullException.ThrowIfNull(argument: request.QualifiedNames[0]);
    }
}