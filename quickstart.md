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

Replace `1.0.0` with a version published by the release workflow:

```bash
dotnet add package SqlStorageEngine \
  --version 1.0.0 \
  --source "https://nuget.pkg.github.com/callummarshall9/index.json"
```

The consuming project must target `.NET 10` or a compatible later framework.

## 3. Create, allocate, reopen, and inspect a database

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

## 4. Publish a package version

No repository secret is required for publication. The workflow uses the automatically created `GITHUB_TOKEN` with
repository-scoped `packages: write` permission.

- Push a SemVer tag such as `v1.0.0`; or
- Open **Actions → Publish NuGet package → Run workflow** and enter `1.0.0`.

The workflow restores, runs the entire Release test suite with warnings treated as errors, creates `.nupkg` and
`.snupkg` artifacts, and pushes both to this repository owner's GitHub Packages feed. Package versions are immutable;
`--skip-duplicate` makes a repeated run harmless but does not replace an existing version.

If organization policy disables package writes for `GITHUB_TOKEN`, enable **Settings → Actions → General → Workflow
permissions → Read and write permissions**. Consumers still authenticate separately with `read:packages`.
