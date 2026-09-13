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
        if (args[1] == "committed") await transaction.CommitAsync();
        else if (args[1] == "nested")
            await transaction.ExecuteStatementAsync((_, _) => { Environment.Exit(42); return ValueTask.CompletedTask; });
        // Deliberately terminate without disposing the engine or transaction.
        Environment.Exit(42);
    }
}
