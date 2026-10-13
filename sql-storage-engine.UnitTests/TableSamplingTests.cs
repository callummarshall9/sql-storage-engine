using sql_storage_engine.Models.Catalog;
// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;

using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class TableSamplingTests
{
    [Test]
    public void SampleContracts_RejectInvalidSizesAndSeeds()
    {
        ((Func<StorageTableSample>)(() => sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStoragePercentTableSample(-0.01m))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<StorageTableSample>)(() => sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStoragePercentTableSample(100.01m))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<StorageTableSample>)(() => sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageRowsTableSample(-1))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<StorageTableSample>)(() => sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageRowsTableSample(1, -1))).Should().Throw<ArgumentOutOfRangeException>();
        sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStoragePercentTableSample(0).Percentage.Should().Be(0);
        sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStoragePercentTableSample(100, 7).RepeatableSeed.Should().Be(7);
        sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageRowsTableSample(0).RowCount.Should().Be(0);
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
                var table = engine.ReadCatalog().ReadTables().Single();
                var rows = await engine.OpenTableAsync(table.Id);
                var all = await CollectAsync(rows.ScanAsync());
                var sample = sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStoragePercentTableSample(35m, repeatableSeed: 42);
                var first = await CollectAsync(rows.SampleAsync(sample));
                var second = await CollectAsync(rows.SampleAsync(sample));
                first.Select(row => row.RowId).Should().Equal(second.Select(row => row.RowId));
                first.Should().NotBeEmpty().And.HaveCountLessThan(all.Count);
                foreach (var sampledPage in first.GroupBy(row => row.RowId.PageId))
                    sampledPage.Select(row => row.RowId).Should().Equal(all.Where(row => row.RowId.PageId == sampledPage.Key).Select(row => row.RowId), "SYSTEM sampling includes every live row from a selected heap page");
                (await CollectAsync(rows.SampleAsync(sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStoragePercentTableSample(0)))).Should().BeEmpty();
                (await CollectAsync(rows.SampleAsync(sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStoragePercentTableSample(100)))).Select(row => row.RowId).Should().Equal(all.Select(row => row.RowId));
                expected = first.Select(row => row.RowId).ToArray();
            }

            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path,sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageEngineOptions()with{BufferPoolCapacity = 16, InlineValueThreshold = 2_000 });
            var reopenedRows = await reopened.OpenTableAsync(reopened.ReadCatalog().ReadTables().Single().Id);
            (await CollectAsync(reopenedRows.SampleAsync(sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStoragePercentTableSample(35m, repeatableSeed: 42)))).Select(row => row.RowId).Should().Equal(expected);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task RowSample_IsPageGranularAndApproximatesRequestedCardinalityAcrossSeeds()
    {
        var path = TemporaryPath();
        try
        {
            await using var engine = await CreatePopulatedEngineAsync(path);
            var rows = await engine.OpenTableAsync(engine.ReadCatalog().ReadTables().Single().Id);
            var all = await CollectAsync(rows.ScanAsync());
            List<int> observed = [];
            for (var seed = 0; seed < 64; seed++)
                observed.Add((await CollectAsync(rows.SampleAsync(sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageRowsTableSample(24, repeatableSeed: seed)))).Count);
            observed.Average().Should().BeInRange(18, 30);
            observed.Should().OnlyContain(count => count >= 0 && count <= all.Count);
            (await CollectAsync(rows.SampleAsync(sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageRowsTableSample(10_000, 1)))).Select(row => row.RowId).Should().Equal(all.Select(row => row.RowId));
            (await CollectAsync(rows.SampleAsync(sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageRowsTableSample(0, 1)))).Should().BeEmpty();
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task Sample_CancellationAndEarlyDisposalReleaseTheStatementGate()
    {
        var path = TemporaryPath();
        try
        {
            await using var engine = await CreatePopulatedEngineAsync(path);
            var rows = await engine.OpenTableAsync(engine.ReadCatalog().ReadTables().Single().Id);
            var cancelled = new CancellationToken(canceled: true);
            await ((Func<Task>)(async () => await CollectAsync(rows.SampleAsync(sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStoragePercentTableSample(50, 1), cancelled)))).Should().ThrowAsync<OperationCanceledException>();
            await foreach (var _ in rows.SampleAsync(sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStoragePercentTableSample(100)))
                break;
            (await CollectAsync(rows.ScanAsync())).Should().HaveCount(120);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Test]
    public async Task Sample_ReadOptionsApplyMaskingWithoutChangingSelectedRows()
    {
        var path = TemporaryPath();
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var table = await engine.CreateCatalogTableAsync("members",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "name", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(20), false, maskingFunction: "partial(1,\"XXXX\",1)")]);
            var rows = await engine.OpenTableAsync(table.Id);
            await rows.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("Callum")]));
            var sample = sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStoragePercentTableSample(100, repeatableSeed: 1);
            var privileged = (await CollectAsync(rows.SampleAsync(sample))).Single();
            var masked = (await CollectAsync(rows.SampleAsync(sample,sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageReadOptions()))).Single();
            masked.RowId.Should().Be(privileged.RowId);
            privileged.Row.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("Callum"));
            masked.Row.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("CXXXXm"));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static async ValueTask<sql_storage_engine.Exposures.Storage.StorageSessionExposure> CreatePopulatedEngineAsync(string path)
    {
        sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path,sql_storage_engine.Exposures.Storage.StorageQueryExposure.CreateStorageEngineOptions()with{BufferPoolCapacity = 16, InlineValueThreshold = 2_000 });
        try
        {
            var table = await engine.CreateCatalogTableAsync("sample_items",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "payload", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarBinarySqlType(1_500), false)]);
            var rows = await engine.OpenTableAsync(table.Id);
            for (var index = 0; index < 120; index++)
                await rows.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(index), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(Enumerable.Repeat((byte)index, 1_200).ToArray())]));
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
        await foreach (var row in source)
            result.Add(row);
        return result;
    }

    private static string TemporaryPath() => Path.Combine(Path.GetTempPath(), $"storage-sample-{Guid.NewGuid():N}.db");
    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
        if (File.Exists(path + ".statement-undo"))
            File.Delete(path + ".statement-undo");
    }
}