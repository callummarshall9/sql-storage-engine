using System.Text.Json;
using sql_storage_engine;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var engine = await StorageEngine.OpenAsync(args[0]);
        var transaction = await engine.BeginTransactionAsync(new(TimeSpan.FromMinutes(1)));
        await File.WriteAllTextAsync(args[0] + ".probe-identity", JsonSerializer.Serialize(transaction.Identity));
        await transaction.ExecuteStatementAsync(async (context, token) =>
        {
            var definition = await context.CreateTableAsync(new CatalogTableName("master", "dbo", "crash_created"),
                [new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false)], cancellationToken: token);
            await (await context.OpenTableAsync(definition.Id, token)).InsertAsync(new Row([SqlValue.Integer(42)]), token);
        });
        if (args[1].StartsWith("security", StringComparison.Ordinal))
        {
            await transaction.ExecuteStatementAsync((c, t) => c.Security.SetPrincipalAsync(new(
                new(Guid.Parse("11111111-2222-3333-4444-555555555555"), Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")), true),
                c.Security.Snapshot.Revision, Guid.NewGuid(), t));
            if (args[1].StartsWith("security-event-", StringComparison.Ordinal))
                engine.TransactionObserver = stage => { if (stage == args[1]) Environment.Exit(42); };
            await engine.Security.AppendEventAsync(new(Guid.NewGuid(), Guid.NewGuid(), null, null,
                sql_storage_engine.Security.StoragePermissionAction.Select, sql_storage_engine.Security.StorageAuditKind.ReadAdmission, 2));
            if (args[1] == "security-committed") await transaction.CommitAsync();
            Environment.Exit(42);
        }
        if (args[1].StartsWith("savepoint", StringComparison.Ordinal))
        {
            var point = await transaction.CreateSavepointAsync("before");
            await transaction.ExecuteStatementAsync(async (context, token) =>
                await context.CreateTableAsync(new CatalogTableName("master", "dbo", "savepoint_later"),
                    [new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false)], cancellationToken: token));
            if (args[1] == "savepoint-rewrite") engine.TransactionObserver = stage => { if (stage == "AfterSavepointRestore") Environment.Exit(42); };
            if (args[1] != "savepoint-active") await transaction.RollbackToSavepointAsync(point);
            if (args[1] == "savepoint-committed") await transaction.CommitAsync();
            Environment.Exit(42);
        }
        if (args[1] == "committed") await transaction.CommitAsync();
        else if (args[1] == "nested")
            await transaction.ExecuteStatementAsync((_, _) => { Environment.Exit(42); return ValueTask.CompletedTask; });
        // Deliberately terminate without disposing the engine or transaction.
        Environment.Exit(42);
    }
}
