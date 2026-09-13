using AwesomeAssertions;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class ExplicitTransactionInterleavedReadTests
{
    [Test]
    public async Task PausedScopedScanAllowsSequentialPointReadsWithoutAllowingConcurrentOperations()
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(10)));
        await transaction.ExecuteStatementAsync(async (context, token) =>
        {
            var table = await context.OpenTableAsync(database.Table.Id, token);
            var index = await context.OpenIndexAsync(database.Index, token);
            var count = 0;
            await foreach (var row in table.ScanAsync(token))
            {
                (await table.GetAsync(row.RowId, token)).Should().NotBeNull();
                (await index.FindAsync([row.Row.Values[0]], token)).Should().ContainSingle();
                count++;
            }
            count.Should().Be(2);
        });
        (await transaction.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
    }
}
