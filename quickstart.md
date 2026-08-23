# SQL Storage Engine quickstart

`SqlStorageEngine` is a .NET 10 library providing page storage, buffer management, heap rows, persistent B+ trees,
transactions, WAL/recovery, backup, integrity, and operational primitives. It is currently a storage-engine library,
not a SQL parser or client/server database.

## 1. Authenticate to the private GitHub NuGet feed

Create a GitHub personal access token (classic) with `read:packages`. If this repository is private, the token also
needs `repo`, and your organization may require SSO authorization. Do not commit the token.

Add the owner-scoped feed once on your development machine:

```bash
dotnet nuget add source \
  "https://nuget.pkg.github.com/callummarshall9/index.json" \
  --name github \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text
```

The clear-text switch is required by the cross-platform NuGet credential store on Linux. Keep the resulting user
NuGet configuration private. In CI, prefer injecting credentials through the
`NuGetPackageSourceCredentials_github` environment variable instead of writing them to a file:

```text
Username=YOUR_GITHUB_USERNAME;Password=YOUR_GITHUB_PAT
```

Your repository `NuGet.config` can then contain only the source URL:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="github"
         value="https://nuget.pkg.github.com/callummarshall9/index.json" />
  </packageSources>
</configuration>
```

## 2. Install the package

Install the sampling- and temporal-capable contract release (or a later compatible version):

```bash
dotnet add package SqlStorageEngine \
  --version 1.7.1 \
  --source "https://nuget.pkg.github.com/callummarshall9/index.json"
```

The consuming project must target `.NET 10` or a compatible later framework.

## 3. Build a SQL engine against the logical API

SQL analyzers and executors should depend on `IStorageEngine`, `IStorageCatalog`, `IStorageTable`, and
`IStorageIndex`. These contracts expose stable schemas, typed values, logical rows, generation-safe row IDs, and
index access without leaking page layouts, buffer pins, codecs, or allocation machinery.

```csharp
using sql_storage_engine;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

await using IStorageEngine storage = await StorageEngine.CreateAsync("example.db");

CatalogTable table = await storage.CreateTableAsync("users",
[
    new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
    new CatalogColumn(new ColumnId(2), "name",
        SqlType.NVarChar(200, "Latin1_General_100_CI_AI"), true)
]);

CatalogIndex index = await storage.CreateIndexAsync("users_by_id", table.Id, true,
    [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.Last)]);

// Semantic analysis: bind names and inspect ordered column/type/nullability metadata.
if (!storage.Catalog.TryGetTable("users", out CatalogTable? bound))
    throw new InvalidOperationException("Unknown table.");

// Execution: use logical rows; the package owns heap, overflow, and index maintenance.
IStorageTable users = await storage.OpenTableAsync(bound.Id);
var rowId = await users.InsertAsync(new Row([SqlValue.Integer(42), SqlValue.Text("Ada")]));
StoredRow? row = await users.GetAsync(rowId);

IStorageIndex usersById = await storage.OpenIndexAsync(index.Id);
IReadOnlyList<Identifiers.RowId> matches = await usersById.FindAsync([SqlValue.Integer(42)]);
```

Native `SYSTEM` table sampling selects complete heap pages. Percent and approximate-row forms are distinct typed
contracts; a repeatable seed reproduces the same selection while the heap layout is unchanged:

```csharp
await foreach (StoredRow sampled in users.SampleAsync(
    new StoragePercentTableSample(10m, repeatableSeed: 42)))
{
    Console.WriteLine(sampled.RowId);
}

await foreach (StoredRow sampled in users.SampleAsync(
    new StorageRowsTableSample(1_000, repeatableSeed: 42)))
{
    Console.WriteLine(sampled.RowId);
}
```

Because sampling is page-granular, a rows sample is approximate and can return more or fewer rows than requested.
Omitting the seed produces a fresh selection for each enumeration. Both sample forms retain scan cancellation, early
disposal, statement coordination, masking, corruption detection, and row-decoding behavior.

System-versioned tables publish an explicit current/history identity and use native temporal scans. The storage engine
generates the two UTC `datetime2` period values and atomically archives the previous version on update or delete:

```csharp
CatalogTable accounts = await storage.CreateSystemVersionedTableAsync(
    new CatalogTableName("example", "dbo", "accounts"),
    [
        new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
        new CatalogColumn(new ColumnId(2), "balance", SqlType.Int, false),
        new CatalogColumn(new ColumnId(3), "valid_from", SqlType.DateTime2(), false,
            generatedAlways: CatalogGeneratedAlwaysKind.RowStart, isHidden: true),
        new CatalogColumn(new ColumnId(4), "valid_to", SqlType.DateTime2(), false,
            generatedAlways: CatalogGeneratedAlwaysKind.RowEnd, isHidden: true)
    ],
    new ColumnId(3),
    new ColumnId(4));

IStorageTable temporalAccounts = await storage.OpenTableAsync(accounts.Id);
await foreach (StorageTemporalRow version in temporalAccounts.TemporalScanAsync(
    new StorageTemporalAsOf(new DateTime(2026, 1, 1))))
{
    Console.WriteLine($"{version.SourceTableId}: {version.RowId}");
}
```

`StorageTemporalAsOf`, `StorageTemporalFromTo`, `StorageTemporalBetweenAnd`,
`StorageTemporalContainedIn`, and `StorageTemporalAll` preserve SQL Server's distinct interval boundaries. A history
table cannot be mutated through its public handle. Direct temporal update/delete calls and multi-mutation statement
scopes both use the durable statement journal, so current and history state recover together after failure or restart.

Database-scoped SQL Server types are created before tables that reference them:

```csharp
SqlType accountNumber = SqlType.Alias(
    "sales", "AccountNumber", SqlType.VarChar(12), isNullable: false);
await storage.CreateScalarTypeAsync(accountNumber);

await storage.CreateTableTypeAsync("sales", "AccountBatch",
[
    new CatalogColumn(new ColumnId(1), "account", accountNumber, false),
    new CatalogColumn(new ColumnId(2), "amount", SqlType.Decimal(38, 4), false)
],
[
    new CatalogTableTypeIndex("PK_AccountBatch", isPrimaryKey: true, isUnique: true,
        [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)])
]);
```

Typed XML collections are registered with `CreateXmlSchemaCollectionAsync` before columns reference
`SqlType.TypedXml`; they can later be extended or dropped when dependency-free. CLR assemblies are cataloged with
`CreateAssemblyAsync`, and trusted executable behavior is bound through `ISqlClrTypeRuntime`. JSON-path, namespace-bound
XML, spatial, and vector indexes use `CreateSpecializedIndexAsync`; opened indexes expose JSON value/existence and XML
path/value seeks in addition to whole-value lookup. The full coverage and normalization matrix is in
[SQL Server 2025 data-type coverage](docs/sql-server-data-types.md).

Column values supplied to an index are in its declared column order. A table scan streams `StoredRow` values; an
index scan streams matching `RowId` values which can be fetched from the owning table. DDL and row mutations are
flushed before they return. Use `ExecuteStatementAsync` when several row mutations must commit as one crash-atomic
statement; the callback can create a table, open statement-scoped tables, and populate the new target so catalog plus
rows are published once or restored together. `IStorageEngine.DatabaseId` and `IStorageCatalog.DatabaseId` expose the
persisted database incarnation, allowing prepared execution bindings to reject a different database even when its
table metadata is byte-for-byte identical.
The lower-level transaction classes remain implementation primitives rather than the executor contract.

Use `TryInsertAsync` when an index declares `IGNORE_DUP_KEY`; its `TableInsertResult` represents either the new `RowId`
or a skipped row and warning. Existing `InsertAsync` remains a strict wrapper. Query executors can request dynamic-data-
masking projection with `StorageReadOptions.Unprivileged`, or pass table/column `UNMASK` grants through the corresponding
read options. The original read overloads represent a privileged/raw storage read.

## 4. Use page primitives for diagnostics and storage development

```csharp
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;

var databasePath = Path.Combine(AppContext.BaseDirectory, "example.db");

// CreateAsync fails if the destination already exists. Writer ownership marks the
// database recovery-required until DisposeAsync completes its ordered clean shutdown.
await using (var database = await PageDatabase.CreateAsync(databasePath))
{
    PageId heapPageId = await database.AllocateAsync(PageType.Heap);

    var page = new byte[database.PageSize];
    await database.ReadAsync(heapPageId, page);

    PageHeader header = PageHeaderCodec.Read(page);
    Console.WriteLine($"Created {header.PageId} as {header.PageType}");
}

// Read-only open never changes the file and requires a prior clean shutdown.
await using var reopened = await PageDatabase.OpenAsync(
    databasePath,
    DatabaseOpenMode.ReadOnly);

Console.WriteLine($"Database: {reopened.Header.DatabaseId}");
Console.WriteLine($"Page size: {reopened.PageSize}");
Console.WriteLine($"Next page: {reopened.Header.NextPageId}");
```

Always dispose writer databases. An interrupted writer intentionally leaves the recovery-required marker set;
read-only open then throws `RecoveryRequiredException` rather than modifying the database.

## 5. Publish a package version

No repository secret is required for publication. The workflow uses the automatically created `GITHUB_TOKEN` with
repository-scoped `packages: write` permission.

- Push a SemVer tag such as `v1.0.0`; or
- Open **Actions → Publish NuGet package → Run workflow** and enter `1.0.0`.

The workflow restores, runs the entire Release test suite with warnings treated as errors, creates `.nupkg` and
`.snupkg` artifacts, and pushes both to this repository owner's GitHub Packages feed. Package versions are immutable;
`--skip-duplicate` makes a repeated run harmless but does not replace an existing version.

If organization policy disables package writes for `GITHUB_TOKEN`, enable **Settings → Actions → General → Workflow
permissions → Read and write permissions**. Consumers still authenticate separately with `read:packages`.
