using sql_storage_engine.Rows;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;

namespace sql_storage_engine;

internal sealed class StorageScopedCatalog(IStorageCatalog owner, Func<bool> active) : IStorageCatalog
{
    private void Check() { if (!active()) throw new InvalidOperationException("The transaction callback has ended."); }
    public DatabaseId DatabaseId { get { Check(); return owner.DatabaseId; } }
    public IReadOnlyList<CatalogTable> Tables { get { Check(); return owner.Tables; } }
    public IReadOnlyList<CatalogIndex> Indexes { get { Check(); return owner.Indexes; } }
    public IReadOnlyList<CatalogScalarType> ScalarTypes { get { Check(); return owner.ScalarTypes; } }
    public IReadOnlyList<CatalogTableType> TableTypes { get { Check(); return owner.TableTypes; } }
    public IReadOnlyList<SqlXmlSchemaCollection> XmlSchemaCollections { get { Check(); return owner.XmlSchemaCollections; } }
    public IReadOnlyList<CatalogAssembly> Assemblies { get { Check(); return owner.Assemblies; } }
    public string DefaultCollation { get { Check(); return owner.DefaultCollation; } }
    public bool TryGetTable(string name, out CatalogTable? table) { Check(); return owner.TryGetTable(name, out table); }
    public bool TryGetTable(CatalogTableName name, out CatalogTable? table) { Check(); return owner.TryGetTable(name, out table); }
    public bool TryGetTable(TableId id, out CatalogTable? table) { Check(); return owner.TryGetTable(id, out table); }
    public bool TryGetTemporalHistory(TableId currentTableId, out CatalogTable? historyTable) { Check(); return owner.TryGetTemporalHistory(currentTableId, out historyTable); }
    public bool TryGetTemporalCurrent(TableId historyTableId, out CatalogTable? currentTable) { Check(); return owner.TryGetTemporalCurrent(historyTableId, out currentTable); }
    public bool TryGetIndex(TableId tableId, string name, out CatalogIndex? index) { Check(); return owner.TryGetIndex(tableId, name, out index); }
    public IReadOnlyList<CatalogIndex> GetIndexes(TableId tableId) { Check(); return owner.GetIndexes(tableId); }
    public bool TryGetScalarType(string schemaName, string name, out CatalogScalarType? type) { Check(); return owner.TryGetScalarType(schemaName, name, out type); }
    public bool TryGetTableType(string schemaName, string name, out CatalogTableType? type) { Check(); return owner.TryGetTableType(schemaName, name, out type); }
    public bool TryGetXmlSchemaCollection(string schemaName, string name, out SqlXmlSchemaCollection? collection) { Check(); return owner.TryGetXmlSchemaCollection(schemaName, name, out collection); }
    public bool TryGetAssembly(string name, out CatalogAssembly? assembly) { Check(); return owner.TryGetAssembly(name, out assembly); }
}
