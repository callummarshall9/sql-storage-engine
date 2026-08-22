using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class SystemVersionedTableTests
{
    private static readonly DateTime First = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Second = First.AddDays(1);
    private static readonly DateTime Third = First.AddDays(2);

    [Test]
    public async Task CurrentAndHistoryIdentityPersistsAndEveryTemporalModeUsesSqlBoundaries()
    {
        var path = TemporaryPath();
        var clock = new ManualTimeProvider(First);
        try
        {
            TableId currentId;
            TableId historyId;
            await using (var engine = await StorageEngine.CreateAsync(path, Options(clock)))
            {
                var definition = await CreateTemporalTableAsync(engine);
                currentId = definition.Id;
                historyId = definition.SystemVersioning!.HistoryTableId;
                engine.Catalog.TryGetTemporalHistory(currentId, out var history).Should().BeTrue();
                history!.Id.Should().Be(historyId);
                engine.Catalog.TryGetTemporalCurrent(historyId, out var current).Should().BeTrue();
                current!.Id.Should().Be(currentId);

                var table = await engine.OpenTableAsync(currentId);
                var rowId = await table.InsertAsync(Input(1, 10));
                clock.SetUtcNow(Second);
                var updated = await table.UpdateAsync(rowId,
                    new RowUpdate([new ColumnUpdate(1, SqlValue.Integer(20))]));
                clock.SetUtcNow(Third);
                (await table.DeleteAsync(updated.CurrentRowId)).Deleted.Should().BeTrue();

                Values(await CollectAsync(table.TemporalScanAsync(new StorageTemporalAsOf(First)))).Should().Equal(10);
                Values(await CollectAsync(table.TemporalScanAsync(new StorageTemporalAsOf(Second)))).Should().Equal(20);
                Values(await CollectAsync(table.TemporalScanAsync(new StorageTemporalAsOf(Third)))).Should().BeEmpty();
                Values(await CollectAsync(table.TemporalScanAsync(new StorageTemporalFromTo(Second, Third))))
                    .Should().Equal(20);
                Values(await CollectAsync(table.TemporalScanAsync(new StorageTemporalBetweenAnd(First, Second))))
                    .Should().Equal(10, 20);
                Values(await CollectAsync(table.TemporalScanAsync(new StorageTemporalContainedIn(First, Third))))
                    .Should().Equal(10, 20);
                var all = await CollectAsync(table.TemporalScanAsync(new StorageTemporalAll()));
                Values(all).Should().Equal(10, 20);
                all.Should().OnlyContain(row => row.SourceTableId == historyId);
            }

            await using var reopened = await StorageEngine.OpenAsync(path, Options(clock));
            reopened.Catalog.TryGetTemporalHistory(currentId, out var reopenedHistory).Should().BeTrue();
            reopenedHistory!.Id.Should().Be(historyId);
            var reopenedTable = await reopened.OpenTableAsync(currentId);
            Values(await CollectAsync(reopenedTable.TemporalScanAsync(new StorageTemporalAll())))
                .Should().Equal(10, 20);
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task FailedUpdateRemovesItsTentativeHistoryVersionAndHistoryIsReadOnly()
    {
        var path = TemporaryPath();
        var clock = new ManualTimeProvider(First);
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path, Options(clock));
            var definition = await CreateTemporalTableAsync(engine);
            var table = await engine.OpenTableAsync(definition.Id);
            var rowId = await table.InsertAsync(Input(1, 10));
            clock.SetUtcNow(Second);

            await ((Func<Task>)(async () => await table.UpdateAsync(rowId,
                    new RowUpdate([new ColumnUpdate(99, SqlValue.Integer(20))]))))
                .Should().ThrowAsync<ArgumentOutOfRangeException>();

            table = await engine.OpenTableAsync(definition.Id);
            var all = await CollectAsync(table.TemporalScanAsync(new StorageTemporalAll()));
            Values(all).Should().Equal(10);
            all.Single().SourceTableId.Should().Be(definition.Id);
            var history = await engine.OpenTableAsync(definition.SystemVersioning!.HistoryTableId);
            await ((Func<Task>)(async () => await history.InsertAsync(Input(2, 20))))
                .Should().ThrowAsync<InvalidOperationException>();
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public async Task TemporalScanObservesPreCancellationAndOrdinaryTablesFailExplicitly()
    {
        var path = TemporaryPath();
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path, Options(new ManualTimeProvider(First)));
            var temporal = await CreateTemporalTableAsync(engine);
            var table = await engine.OpenTableAsync(temporal.Id);
            await table.InsertAsync(Input(1, 10));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await ((Func<Task>)(async () => await CollectAsync(
                    table.TemporalScanAsync(new StorageTemporalAll(), canceled.Token))))
                .Should().ThrowAsync<OperationCanceledException>();

            var ordinary = await engine.CreateTableAsync("ordinary", [
                new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false)]);
            var ordinaryTable = await engine.OpenTableAsync(ordinary.Id);
            await ((Func<Task>)(async () => await CollectAsync(
                    ordinaryTable.TemporalScanAsync(new StorageTemporalAll()))))
                .Should().ThrowAsync<InvalidOperationException>();
        }
        finally { DeleteDatabase(path); }
    }

    [Test]
    public void CatalogRejectsMismatchedHistorySchema()
    {
        var currentId = new TableId(1);
        var historyId = new TableId(2);
        var columns = Columns();
        var current = new CatalogTable(currentId, "items", 1, new PageId(1), columns,
            systemVersioning: new CatalogSystemVersioning(historyId, new ColumnId(3), new ColumnId(4)));
        var history = new CatalogTable(historyId, "itemsHistory", 1, new PageId(2),
            columns.Select(column => column.Id == new ColumnId(2)
                ? new CatalogColumn(column.Id, column.Name, SqlType.BigInt, column.IsNullable)
                : column));

        ((Action)(() => _ = new CatalogDefinition([current, history], [])))
            .Should().Throw<ArgumentException>().WithMessage("*ordered column schema*");
    }

    private static async ValueTask<CatalogTable> CreateTemporalTableAsync(StorageEngine engine) =>
        await engine.CreateSystemVersionedTableAsync(
            new CatalogTableName("app", "dbo", "items"), Columns(), new ColumnId(3), new ColumnId(4));

    private static CatalogColumn[] Columns() =>
    [
        new(new ColumnId(1), "id", SqlType.Int, false),
        new(new ColumnId(2), "value", SqlType.Int, false),
        new(new ColumnId(3), "valid_from", SqlType.DateTime2(), false,
            generatedAlways: CatalogGeneratedAlwaysKind.RowStart, isHidden: true),
        new(new ColumnId(4), "valid_to", SqlType.DateTime2(), false,
            generatedAlways: CatalogGeneratedAlwaysKind.RowEnd, isHidden: true)
    ];

    private static Row Input(long id, long value) => new([
        SqlValue.Integer(id), SqlValue.Integer(value), SqlValue.Default, SqlValue.Default]);

    private static long[] Values(IEnumerable<StorageTemporalRow> rows) => rows
        .Select(row => ((IntegerSqlValue)row.Row.Values[1]).Value).ToArray();

    private static async Task<List<StorageTemporalRow>> CollectAsync(IAsyncEnumerable<StorageTemporalRow> rows)
    {
        List<StorageTemporalRow> result = [];
        await foreach (var row in rows) result.Add(row);
        return result;
    }

    private static StorageEngineOptions Options(TimeProvider clock) => new() { TimeProvider = clock };
    private static string TemporaryPath() => Path.Combine(Path.GetTempPath(), $"temporal-{Guid.NewGuid():N}.db");
    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".statement-undo")) File.Delete(path + ".statement-undo");
    }

    private sealed class ManualTimeProvider(DateTime start) : TimeProvider
    {
        private DateTimeOffset _utcNow = new(start);
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void SetUtcNow(DateTime value) => _utcNow = new DateTimeOffset(value);
    }
}
