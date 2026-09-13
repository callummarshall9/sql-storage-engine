using AwesomeAssertions;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class ExplicitTransactionIndexTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task InsertDeleteAndKeyChangesResolveWithTheirIndexEntries(bool commit)
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await database.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(10)));
        await transaction.ExecuteStatementAsync(async (context, token) =>
        {
            var table = await context.OpenTableAsync(database.Table.Id, token);
            await table.UpdateAsync(database.First, new RowUpdate([new ColumnUpdate(0, SqlValue.Integer(3))]), token);
            await table.DeleteAsync(database.Second, token);
            await table.InsertAsync(new Row([SqlValue.Integer(4), SqlValue.Integer(100)]), token);
            var index = await context.OpenIndexAsync(database.Index, token);
            (await index.FindAsync([SqlValue.Integer(1)], token)).Should().BeEmpty();
            (await index.FindAsync([SqlValue.Integer(2)], token)).Should().BeEmpty();
            (await index.FindAsync([SqlValue.Integer(3)], token)).Should().ContainSingle();
            (await index.FindAsync([SqlValue.Integer(4)], token)).Should().ContainSingle();
        });
        if (commit) await transaction.CommitAsync(); else await transaction.RollbackAsync();
        await database.ReopenAsync();
        var index = await database.Engine.OpenIndexAsync(database.Index);
        foreach (var value in new[] { 1, 2, 3, 4 })
            (await index.FindAsync([SqlValue.Integer(value)])).Count.Should().Be((value >= 3) == commit ? 1 : 0);
    }
}
