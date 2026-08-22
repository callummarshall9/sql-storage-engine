using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class TableSamplingTests
{
    [Test]
    public void SampleContracts_RejectInvalidSizesAndSeeds()
    {
        ((Func<StorageTableSample>)(() => new StoragePercentTableSample(-0.01m)))
            .Should().Throw<ArgumentOutOfRangeException>();
        ((Func<StorageTableSample>)(() => new StoragePercentTableSample(100.01m)))
            .Should().Throw<ArgumentOutOfRangeException>();
        ((Func<StorageTableSample>)(() => new StorageRowsTableSample(-1)))
            .Should().Throw<ArgumentOutOfRangeException>();
        ((Func<StorageTableSample>)(() => new StorageRowsTableSample(1, -1)))
            .Should().Throw<ArgumentOutOfRangeException>();

        new StoragePercentTableSample(0).Percentage.Should().Be(0);
        new StoragePercentTableSample(100, 7).RepeatableSeed.Should().Be(7);
        new StorageRowsTableSample(0).RowCount.Should().Be(0);
    }

    [Test]
    public async Task PercentSample_SelectsWholePagesAndRepeatableSeedSurvivesReopen()
    {
        var path = TemporaryPath();
        IReadOnlyList<RowId> expected;
        try
        {
            await using (var engine = await CreatePopulatedEngineAsync(path))
            {
                var table = engine.Catalog.Tables.Single();
                var rows = await engine.OpenTableAsync(table.Id);
                var all = await CollectAsync(rows.ScanAsync());
                var sample = new StoragePercentTableSample(35m, repeatableSeed: 42);

                var first = await CollectAsync(rows.SampleAsync(sample));
                var second = await CollectAsync(rows.SampleAsync(sample));

                first.Select(row => row.RowId).Should().Equal(second.Select(row => row.RowId));
                first.Should().NotBeEmpty().And.HaveCountLessThan(all.Count);
                foreach (var sampledPage in first.GroupBy(row => row.RowId.PageId))
                    sampledPage.Select(row => row.RowId).Should().Equal(
                        all.Where(row => row.RowId.PageId == sampledPage.Key).Select(row => row.RowId),
                        "SYSTEM sampling includes every live row from a selected heap page");

                (await CollectAsync(rows.SampleAsync(new StoragePercentTableSample(0)))).Should().BeEmpty();
                (await CollectAsync(rows.SampleAsync(new StoragePercentTableSample(100))))
                    .Select(row => row.RowId).Should().Equal(all.Select(row => row.RowId));
                expected = first.Select(row => row.RowId).ToArray();
            }

            await using var reopened = await StorageEngine.OpenAsync(path,
                new StorageEngineOptions { BufferPoolCapacity = 16, InlineValueThreshold = 2_000 });
            var reopenedRows = await reopened.OpenTableAsync(reopened.Catalog.Tables.Single().Id);
            (await CollectAsync(reopenedRows.SampleAsync(
                    new StoragePercentTableSample(35m, repeatableSeed: 42))))
                .Select(row => row.RowId).Should().Equal(expected);
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task RowSample_IsPageGranularAndApproximatesRequestedCardinalityAcrossSeeds()
    {
        var path = TemporaryPath();
        try
        {
            await using var engine = await CreatePopulatedEngineAsync(path);
            var rows = await engine.OpenTableAsync(engine.Catalog.Tables.Single().Id);
            var all = await CollectAsync(rows.ScanAsync());
            List<int> observed = [];
            for (var seed = 0; seed < 64; seed++)
                observed.Add((await CollectAsync(rows.SampleAsync(
                    new StorageRowsTableSample(24, repeatableSeed: seed)))).Count);

            observed.Average().Should().BeInRange(18, 30);
            observed.Should().OnlyContain(count => count >= 0 && count <= all.Count);
            (await CollectAsync(rows.SampleAsync(new StorageRowsTableSample(10_000, 1))))
                .Select(row => row.RowId).Should().Equal(all.Select(row => row.RowId));
            (await CollectAsync(rows.SampleAsync(new StorageRowsTableSample(0, 1)))).Should().BeEmpty();
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task Sample_CancellationAndEarlyDisposalReleaseTheStatementGate()
    {
        var path = TemporaryPath();
        try
        {
            await using var engine = await CreatePopulatedEngineAsync(path);
            var rows = await engine.OpenTableAsync(engine.Catalog.Tables.Single().Id);
            var cancelled = new CancellationToken(canceled: true);

            await ((Func<Task>)(async () => await CollectAsync(
                    rows.SampleAsync(new StoragePercentTableSample(50, 1), cancelled))))
                .Should().ThrowAsync<OperationCanceledException>();

            await foreach (var _ in rows.SampleAsync(new StoragePercentTableSample(100))) break;
            (await CollectAsync(rows.ScanAsync())).Should().HaveCount(120);
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task Sample_ReadOptionsApplyMaskingWithoutChangingSelectedRows()
    {
        var path = TemporaryPath();
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var table = await engine.CreateTableAsync("members",
            [
                new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
                new CatalogColumn(new ColumnId(2), "name", SqlType.NVarChar(20), false,
                    maskingFunction: "partial(1,\"XXXX\",1)")
            ]);
            var rows = await engine.OpenTableAsync(table.Id);
            await rows.InsertAsync(new Row([SqlValue.Integer(1), SqlValue.Text("Callum")]));
            var sample = new StoragePercentTableSample(100, repeatableSeed: 1);

            var privileged = (await CollectAsync(rows.SampleAsync(sample))).Single();
            var masked = (await CollectAsync(rows.SampleAsync(sample, StorageReadOptions.Unprivileged))).Single();

            masked.RowId.Should().Be(privileged.RowId);
            privileged.Row.Values[1].Should().Be(SqlValue.Text("Callum"));
            masked.Row.Values[1].Should().Be(SqlValue.Text("CXXXXm"));
        }
        finally { DeleteDatabase(path); }
    }

    private static async ValueTask<StorageEngine> CreatePopulatedEngineAsync(string path)
    {
        var engine = await StorageEngine.CreateAsync(path,
            new StorageEngineOptions { BufferPoolCapacity = 16, InlineValueThreshold = 2_000 });
        try
        {
            var table = await engine.CreateTableAsync("sample_items",
            [
                new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
                new CatalogColumn(new ColumnId(2), "payload", SqlType.VarBinary(1_500), false)
            ]);
            var rows = await engine.OpenTableAsync(table.Id);
            for (var index = 0; index < 120; index++)
                await rows.InsertAsync(new Row([
                    SqlValue.Integer(index),
                    SqlValue.Binary(Enumerable.Repeat((byte)index, 1_200).ToArray())
                ]));
            return engine;
        }
        catch
        {
            await engine.DisposeAsync();
            throw;
        }
    }

    private static async Task<List<StoredRow>> CollectAsync(IAsyncEnumerable<StoredRow> source)
    {
        List<StoredRow> result = [];
        await foreach (var row in source) result.Add(row);
        return result;
    }

    private static string TemporaryPath() => Path.Combine(Path.GetTempPath(), $"storage-sample-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".statement-undo")) File.Delete(path + ".statement-undo");
    }
}
