// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Tables;

namespace sql_storage_engine.Models.Storage;
public sealed class StorageSessionContext
{
    internal StorageGraphContext? Graph { get; set; }
    internal StorageEngineObserverContext? Observer { get; init; }
    internal StoragePublicTransactionContext? Transaction { get; init; }
    internal StorageTransactionRequestContext? TransactionRequest { get; init; }
    internal StorageEngineExecutionContext? Execution { get; init; }
    internal StorageStatementRuntime? Statement { get; init; }
    internal StorageEngineCreationContext? Creation { get; init; }
    internal StorageEngineOpenHandleContext? OpenHandle { get; init; }
    internal EngineCatalogContext? EngineCatalog { get; set; }
    internal CatalogSessionContext? Catalog { get; init; }
    internal StoragePublicTableContext? PublicTable { get; init; }
    internal StorageIndexContext? Index { get; init; }
    internal StorageEngineRuntime? Engine { get; init; }
    internal TableStorageContext? Table { get; set; }
    public StorageSessionBinding? Binding { get; set; }
    internal StorageSecurityRequestContext? SecurityRequest { get; init; }
}