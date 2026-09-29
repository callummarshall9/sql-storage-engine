using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using SqlExecutionEngine.Storage.Abstractions;
using SqlStorageEngine.Core;
using sql_storage_engine;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

var directory = Path.Combine(Path.GetTempPath(), "split-consumer-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var factory = new PageBackendFactory(_ => Path.Combine(directory, "raw"));
    OperationId operation = new(Guid.NewGuid());
    CollectionId collection = new(Guid.NewGuid());
    await using (var store = await factory.OpenAsync(new(new([1]), BackendOpenMode.CreateNew)))
    {
        await using var snapshot = await store.OpenSnapshotAsync();
        if ((await store.CommitAsync(new(operation, store.Id, snapshot.Generation,
                [new(collection, new([1]), new([42]))]))).Status != CommitStatus.Committed) throw new Exception("Commit failed.");
    }
    await using (var store = await factory.OpenAsync(new(new([1]), BackendOpenMode.OpenExisting)))
    {
        if ((await store.ResolveAsync(operation)).Status != CommitStatus.Committed) throw new Exception("Receipt lost.");
        await using var snapshot = await store.OpenSnapshotAsync();
        if (!(await snapshot.ReadAsync(collection, new([1])))!.ToArray().SequenceEqual(new byte[] { 42 })) throw new Exception("Value lost.");
    }
    var sqlPath = Path.Combine(directory, "sql");
    TableId id;
    await using (var engine = await StorageEngine.CreateAsync(sqlPath))
    {
        var table = await engine.CreateTableAsync(new CatalogTableName("master", "dbo", "consumer"),
            [new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false)]);
        id = table.Id;
        await (await engine.OpenTableAsync(id)).InsertAsync(new Row([SqlValue.Integer(42)]));
    }
    await using (var engine = await StorageEngine.OpenAsync(sqlPath))
    {
        var count = 0;
        await foreach (var row in (await engine.OpenTableAsync(id)).ScanAsync())
        {
            if (!row.Row.Values[0].Equals(SqlValue.Integer(42))) throw new Exception("SQL value lost.");
            count++;
        }
        if (count != 1) throw new Exception("SQL row lost.");
    }
    var core = typeof(PageBackendFactory).Assembly;
    var compatibility = typeof(StorageEngine).Assembly;
    var current = compatibility.GetExportedTypes().Concat(core.GetExportedTypes()).ToDictionary(t => t.FullName!);
    Console.WriteLine($"core types={core.GetExportedTypes().Length}, compatibility types={compatibility.GetExportedTypes().Length}");
    Console.WriteLine("core API SHA256=" + Hash(core.GetExportedTypes()));
    if (args.Length > 0)
    {
        var context = new AssemblyLoadContext("baseline", isCollectible: true);
        var baseline = context.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
        foreach (var type in baseline.GetExportedTypes())
        {
            if (!current.TryGetValue(type.FullName!, out var retained) || Shape(type) != Shape(retained))
                throw new Exception("Legacy public API changed: " + type.FullName);
        }
        Console.WriteLine($"Retained all {baseline.GetExportedTypes().Length} baseline types and public declared members.");
        context.Unload();
    }
    if (args.Length == 3)
    {
        var binary = Assembly.LoadFrom(Path.GetFullPath(args[1]));
        await (Task)binary.GetType("LegacyBinaryProbe")!.GetMethod("ValidateAsync")!.Invoke(null, [args[2]])!;
        Console.WriteLine("Baseline-compiled binary opened the baseline SQL file through compatibility and forwarded physical types.");
    }
    Console.WriteLine("Package consumers passed: raw commit/recovery and legacy SQL create/reopen.");
}
finally { Directory.Delete(directory, true); }

static string Shape(Type type) => type.FullName + "\n" + string.Join("\n", type.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
    .Select(m => m.MemberType + ":" + m).Order(StringComparer.Ordinal));
static string Hash(IEnumerable<Type> types) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", types.OrderBy(t => t.FullName, StringComparer.Ordinal).Select(Shape)))));
