// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Rows;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Exposures.Rows;

namespace sql_storage_engine.UnitTests;
public sealed class ExplicitTransactionBoundaryTests
{
    [TestCase(0L, false)]
    [TestCase(1L, true)]
    [TestCase(864000000000L, true)]
    [TestCase(864000000001L, false)]
    public void TimeoutHasExactTickBoundaries(long ticks, bool valid)
    {
        Action create = () => _ = StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromTicks(ticks));
        if (valid)
            create.Should().NotThrow();
        else
            create.Should().Throw<StorageModelValidationException>().Which.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task JournalQuotaRejectsWithoutRetainingTheLeaseAndExactSizeCanBegin()
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        await database.Engine.FlushAsync();
        var size = new FileInfo(database.Path).Length;
        Func<Task> begin = async () => await database.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(5), maximumJournalBytes: size - 1));
        await begin.Should().ThrowAsync<InvalidOperationException>();
        await using var transaction = await database.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(5), maximumJournalBytes: size));
        (await transaction.RollbackAsync()).State.Should().Be(StorageTransactionState.Aborted);
    }

    [Test]
    public async Task ReturningWithAnOpenStreamClosesItAndAllowsRootResolution()
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        await using var transaction = await database.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(5)));
        IAsyncEnumerator<StoredRow>? stream = null;
        await transaction.ExecuteStatementAsync(async (context, token) =>
        {
            stream = (await context.OpenTableAsync(database.Table.Id, token)).ScanAsync(token).GetAsyncEnumerator(token);
            (await stream.MoveNextAsync()).Should().BeTrue();
        });
        Func<Task> move = async () => await stream!.MoveNextAsync();
        await move.Should().ThrowAsync<InvalidOperationException>();
        await stream!.DisposeAsync();
        (await transaction.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
    }

    [Test]
    public async Task EngineDisposalRollsBackRootAndSecondOpenCannotRecoverLiveJournal()
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        var transaction = await database.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(5)));
        await transaction.ExecuteStatementAsync(async (context, token) => await (await context.OpenTableAsync(database.Table.Id, token)).UpdateAsync(database.First, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(75) }]), token));
        Func<Task> open = async () => await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(database.Path);
        await open.Should().ThrowAsync<IOException>();
        await database.ReopenAsync();
        (await database.Engine.ResolveTransactionAsync(transaction.ReadIdentity())).State.Should().Be(StorageTransactionState.Aborted);
        (await database.BalancesAsync()).Should().Equal(100, 100);
        await transaction.DisposeAsync();
    }
}