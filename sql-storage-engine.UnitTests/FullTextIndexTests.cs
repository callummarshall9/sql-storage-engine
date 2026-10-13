// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class FullTextIndexTests
{
    private const string Collation = "Latin1_General_100_CI_AI";
    [Test]
    public void OptionsRejectUnsupportedLinguisticAndResourceIdentities()
    {
        ((Func<CatalogFullTextIndexOptions>)(() => sql_storage_engine.Exposures.Catalog.CatalogFullTextIndexOptionsExposure.CreateCatalogFullTextIndexOptions(Collation,language: "en-US"))).Should().Throw<ArgumentException>().WithParameterName("language");
        ((Func<CatalogFullTextIndexOptions>)(() => sql_storage_engine.Exposures.Catalog.CatalogFullTextIndexOptionsExposure.CreateCatalogFullTextIndexOptions(Collation,tokenizer: "host-tokenizer"))).Should().Throw<ArgumentException>().WithParameterName("tokenizer");
        ((Func<CatalogFullTextIndexOptions>)(() => sql_storage_engine.Exposures.Catalog.CatalogFullTextIndexOptionsExposure.CreateCatalogFullTextIndexOptions(Collation,stoplist: "system"))).Should().Throw<ArgumentException>().WithParameterName("stoplist");
        ((Func<CatalogFullTextIndexOptions>)(() => sql_storage_engine.Exposures.Catalog.CatalogFullTextIndexOptionsExposure.CreateCatalogFullTextIndexOptions(Collation,maximumTokensPerDocument: 0))).Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maximumTokensPerDocument");
        ((Func<CatalogFullTextIndexOptions>)(() => sql_storage_engine.Exposures.Catalog.CatalogFullTextIndexOptionsExposure.CreateCatalogFullTextIndexOptions(Collation,maximumTokenLength: CatalogFullTextIndexOptions.MaximumSupportedTokenLength + 1))).Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maximumTokenLength");
    }

    [Test]
    public async Task ExactTermIndexBuildsMaintainsReopensCancelsAndRecovers()
    {
        var path = Path.Combine(Path.GetTempPath(), $"full-text-{Guid.NewGuid():N}.db");
        RowId first;
        RowId second;
        RowId mutable;
        try
        {
            await using (var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var table = await CreateTableWithPrimaryKeyAsync(engine);
                var storage = await engine.OpenTableAsync(table.Id);
                first = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("Café café runner")]));
                second = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("CAFE other")]));
                await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()]));
                var definition = await engine.CreateSpecializedCatalogIndexAsync("documents_terms", table.Id, sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.Last), sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateFullTextCatalogSpecializedIndexOptions(sql_storage_engine.Exposures.Catalog.CatalogFullTextIndexOptionsExposure.CreateCatalogFullTextIndexOptions(Collation)));
                var index = await engine.OpenIndexAsync(definition.Id);
                var cafe = await SearchAsync(index, new FullTextSearchRequest("cafe"));
                cafe.Select(match => match.RowId).Should().Equal(first, second);
                cafe.Should().OnlyContain(match => match.Rank == null);
                mutable = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(4), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("hello hello")]));
                (await SearchAsync(index, new FullTextSearchRequest("hello"))).Select(match => match.RowId).Should().Equal(mutable);
                await storage.UpdateAsync(mutable, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("world") }]));
                (await SearchAsync(index, new FullTextSearchRequest("hello"))).Should().BeEmpty();
                (await SearchAsync(index, new FullTextSearchRequest("world"))).Select(match => match.RowId).Should().Equal(mutable);
                await storage.DeleteAsync(second);
                (await SearchAsync(index, new FullTextSearchRequest("cafe"))).Select(match => match.RowId).Should().Equal(first);
                await ((Func<Task>)(async () => await SearchAsync(index, new FullTextSearchRequest("two terms")))).Should().ThrowAsync<ArgumentException>().WithParameterName("term");
                await ((Func<Task>)(async () => await SearchAsync(index, new FullTextSearchRequest("world", "en-US")))).Should().ThrowAsync<ArgumentException>().WithParameterName("fullTextSearchRequest");
                using var cancelled = new CancellationTokenSource();
                await cancelled.CancelAsync();
                await ((Func<Task>)(async () => await SearchAsync(index, new FullTextSearchRequest("world"), cancelled.Token))).Should().ThrowAsync<OperationCanceledException>();
                await using (var enumerator = index.SearchFullTextAsync(new FullTextSearchRequest("cafe")).GetAsyncEnumerator())
                    (await enumerator.MoveNextAsync()).Should().BeTrue();
                (await SearchAsync(index, new FullTextSearchRequest("world"))).Should().HaveCount(1);
            }

            await using var reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var persisted = reopened.ReadCatalog().ReadIndexes().Single(index => index.Method == CatalogIndexMethod.FullText);
            var options = persisted.SpecializedOptions!.FullTextOptions!;
            options.Collation.Should().Be(Collation);
            options.Language.Should().Be(CatalogFullTextIndexOptions.UndefinedLanguage);
            options.Tokenizer.Should().Be(CatalogFullTextIndexOptions.UnicodeWordTokenizer);
            options.TokenizerVersion.Should().Be(CatalogFullTextIndexOptions.UnicodeWordTokenizerVersion);
            options.Stoplist.Should().Be(CatalogFullTextIndexOptions.NoStoplist);
            options.StoplistVersion.Should().Be(CatalogFullTextIndexOptions.NoStoplistVersion);
            options.Consistency.Should().Be(CatalogFullTextConsistency.Transactional);
            var reopenedIndex = await reopened.OpenIndexAsync(persisted.Id);
            (await SearchAsync(reopenedIndex, new FullTextSearchRequest("world"))).Select(match => match.RowId).Should().Equal(mutable);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public async Task ResourceAndCollationFailuresDoNotPublishOrCorruptTheIndex()
    {
        var path = Path.Combine(Path.GetTempPath(), $"full-text-limits-{Guid.NewGuid():N}.db");
        try
        {
            await using var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var table = await CreateTableWithPrimaryKeyAsync(engine);
            var storage = await engine.OpenTableAsync(table.Id);
            var original = await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("one two three")]));
            await ((Func<Task>)(async () => await engine.CreateSpecializedCatalogIndexAsync("wrong_collation", table.Id, sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.Last), sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateFullTextCatalogSpecializedIndexOptions(sql_storage_engine.Exposures.Catalog.CatalogFullTextIndexOptionsExposure.CreateCatalogFullTextIndexOptions("Latin1_General_100_CS_AS"))))).Should().ThrowAsync<ArgumentException>();
            engine.ReadCatalog().ReadIndexes().Should().NotContain(index => index.Name == "wrong_collation");
            var buildFailure = await ((Func<Task>)(async () => await engine.CreateSpecializedCatalogIndexAsync("too_small", table.Id, sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.Last), sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateFullTextCatalogSpecializedIndexOptions(sql_storage_engine.Exposures.Catalog.CatalogFullTextIndexOptionsExposure.CreateCatalogFullTextIndexOptions(Collation,maximumTokensPerDocument: 2))))).Should().ThrowAsync<IndexBuildException>();
            buildFailure.Which.InnerException.Should().BeOfType<StorageResourceExhaustedException>();
            engine.ReadCatalog().ReadIndexes().Should().NotContain(index => index.Name == "too_small");
            var definition = await engine.CreateSpecializedCatalogIndexAsync("bounded_terms", table.Id, sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },SortDirection.Ascending,NullSortOrder.Last), sql_storage_engine.Exposures.Catalog.CatalogSpecializedIndexOptionsExposure.CreateFullTextCatalogSpecializedIndexOptions(sql_storage_engine.Exposures.Catalog.CatalogFullTextIndexOptionsExposure.CreateCatalogFullTextIndexOptions(Collation,maximumTokensPerDocument: 3,maximumTokenLength: 8)));
            var index = await engine.OpenIndexAsync(definition.Id);
            var insertFailure = await ((Func<Task>)(async () => await storage.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("one two three four")])))).Should().ThrowAsync<TableMutationException>();
            insertFailure.Which.InnerException.Should().BeOfType<StorageResourceExhaustedException>();
            (await SearchAsync(index, new FullTextSearchRequest("four"))).Should().BeEmpty();
            var updateFailure = await ((Func<Task>)(async () => await storage.UpdateAsync(original, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("tokenlength") }])))).Should().ThrowAsync<TableMutationException>();
            updateFailure.Which.InnerException.Should().BeOfType<StorageResourceExhaustedException>();
            (await SearchAsync(index, new FullTextSearchRequest("one"))).Select(match => match.RowId).Should().Equal(original);
            var stored = await storage.GetAsync(original);
            ((TextSqlValue)stored!.Row.Values[1]).Value.Should().Be("one two three");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static async Task<CatalogTable> CreateTableWithPrimaryKeyAsync(IStorageEngine engine)
    {
        var table = await engine.CreateCatalogTableAsync("documents",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "body", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharMaxSqlType(Collation), true)]);
        await engine.CreateCatalogIndexAsync("documents_pk",table.Id,true,[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.First)],sql_storage_engine.Exposures.Catalog.CatalogBTreeIndexOptionsExposure.CreateCatalogBTreeIndexOptions(CatalogIndexStorageKind.Clustered,isPrimaryKey: true));
        return table;
    }

    private static async Task<IReadOnlyList<FullTextIndexMatch>> SearchAsync(IStorageIndex index, FullTextSearchRequest request, CancellationToken cancellationToken = default)
    {
        List<FullTextIndexMatch> matches = [];
        await foreach (var match in index.SearchFullTextAsync(request, cancellationToken))
            matches.Add(match);
        return matches;
    }
}