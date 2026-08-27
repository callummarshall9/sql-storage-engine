using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;

namespace sql_storage_engine.UnitTests;

public sealed class FullTextIndexTests
{
    private const string Collation = "Latin1_General_100_CI_AI";

    [Test]
    public void OptionsRejectUnsupportedLinguisticAndResourceIdentities()
    {
        ((Func<CatalogFullTextIndexOptions>)(() => new CatalogFullTextIndexOptions(Collation, language: "en-US")))
            .Should().Throw<ArgumentException>().WithParameterName("language");
        ((Func<CatalogFullTextIndexOptions>)(() => new CatalogFullTextIndexOptions(Collation,
                tokenizer: "host-tokenizer")))
            .Should().Throw<ArgumentException>().WithParameterName("tokenizer");
        ((Func<CatalogFullTextIndexOptions>)(() => new CatalogFullTextIndexOptions(Collation,
                stoplist: "system")))
            .Should().Throw<ArgumentException>().WithParameterName("stoplist");
        ((Func<CatalogFullTextIndexOptions>)(() => new CatalogFullTextIndexOptions(Collation,
                maximumTokensPerDocument: 0)))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maximumTokensPerDocument");
        ((Func<CatalogFullTextIndexOptions>)(() => new CatalogFullTextIndexOptions(Collation,
                maximumTokenLength: CatalogFullTextIndexOptions.MaximumSupportedTokenLength + 1)))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maximumTokenLength");
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
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var table = await CreateTableWithPrimaryKeyAsync(engine);
                var storage = await engine.OpenTableAsync(table.Id);
                first = await storage.InsertAsync(new Row([
                    SqlValue.Integer(1), SqlValue.Text("Café café runner")
                ]));
                second = await storage.InsertAsync(new Row([
                    SqlValue.Integer(2), SqlValue.Text("CAFE other")
                ]));
                await storage.InsertAsync(new Row([SqlValue.Integer(3), SqlValue.Null]));
                var definition = await engine.CreateSpecializedIndexAsync("documents_terms", table.Id,
                    new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.Last),
                    CatalogSpecializedIndexOptions.FullText(new CatalogFullTextIndexOptions(Collation)));
                var index = await engine.OpenIndexAsync(definition.Id);

                var cafe = await SearchAsync(index, new FullTextSearchRequest("cafe"));
                cafe.Select(match => match.RowId).Should().Equal(first, second);
                cafe.Should().OnlyContain(match => match.Rank == null);

                mutable = await storage.InsertAsync(new Row([
                    SqlValue.Integer(4), SqlValue.Text("hello hello")
                ]));
                (await SearchAsync(index, new FullTextSearchRequest("hello")))
                    .Select(match => match.RowId).Should().Equal(mutable);
                await storage.UpdateAsync(mutable, new RowUpdate([
                    new ColumnUpdate(1, SqlValue.Text("world"))
                ]));
                (await SearchAsync(index, new FullTextSearchRequest("hello"))).Should().BeEmpty();
                (await SearchAsync(index, new FullTextSearchRequest("world")))
                    .Select(match => match.RowId).Should().Equal(mutable);

                await storage.DeleteAsync(second);
                (await SearchAsync(index, new FullTextSearchRequest("cafe")))
                    .Select(match => match.RowId).Should().Equal(first);

                await ((Func<Task>)(async () => await SearchAsync(index,
                        new FullTextSearchRequest("two terms"))))
                    .Should().ThrowAsync<ArgumentException>().WithParameterName("term");
                await ((Func<Task>)(async () => await SearchAsync(index,
                        new FullTextSearchRequest("world", "en-US"))))
                    .Should().ThrowAsync<ArgumentException>().WithParameterName("request");
                using var cancelled = new CancellationTokenSource();
                await cancelled.CancelAsync();
                await ((Func<Task>)(async () => await SearchAsync(index,
                        new FullTextSearchRequest("world"), cancelled.Token)))
                    .Should().ThrowAsync<OperationCanceledException>();

                await using (var enumerator = index.SearchFullTextAsync(new FullTextSearchRequest("cafe"))
                                 .GetAsyncEnumerator())
                    (await enumerator.MoveNextAsync()).Should().BeTrue();
                (await SearchAsync(index, new FullTextSearchRequest("world"))).Should().HaveCount(1);
            }

            await using var reopened = await StorageEngine.OpenAsync(path);
            var persisted = reopened.Catalog.Indexes.Single(index => index.Method == CatalogIndexMethod.FullText);
            var options = persisted.SpecializedOptions!.FullTextOptions!;
            options.Collation.Should().Be(Collation);
            options.Language.Should().Be(CatalogFullTextIndexOptions.UndefinedLanguage);
            options.Tokenizer.Should().Be(CatalogFullTextIndexOptions.UnicodeWordTokenizer);
            options.TokenizerVersion.Should().Be(CatalogFullTextIndexOptions.UnicodeWordTokenizerVersion);
            options.Stoplist.Should().Be(CatalogFullTextIndexOptions.NoStoplist);
            options.StoplistVersion.Should().Be(CatalogFullTextIndexOptions.NoStoplistVersion);
            options.Consistency.Should().Be(CatalogFullTextConsistency.Transactional);
            var reopenedIndex = await reopened.OpenIndexAsync(persisted.Id);
            (await SearchAsync(reopenedIndex, new FullTextSearchRequest("world")))
                .Select(match => match.RowId).Should().Equal(mutable);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public async Task ResourceAndCollationFailuresDoNotPublishOrCorruptTheIndex()
    {
        var path = Path.Combine(Path.GetTempPath(), $"full-text-limits-{Guid.NewGuid():N}.db");
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var table = await CreateTableWithPrimaryKeyAsync(engine);
            var storage = await engine.OpenTableAsync(table.Id);
            var original = await storage.InsertAsync(new Row([
                SqlValue.Integer(1), SqlValue.Text("one two three")
            ]));

            await ((Func<Task>)(async () => await engine.CreateSpecializedIndexAsync("wrong_collation", table.Id,
                    new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.Last),
                    CatalogSpecializedIndexOptions.FullText(
                        new CatalogFullTextIndexOptions("Latin1_General_100_CS_AS")))))
                .Should().ThrowAsync<ArgumentException>();
            engine.Catalog.Indexes.Should().NotContain(index => index.Name == "wrong_collation");

            var buildFailure = await ((Func<Task>)(async () => await engine.CreateSpecializedIndexAsync("too_small", table.Id,
                    new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.Last),
                    CatalogSpecializedIndexOptions.FullText(new CatalogFullTextIndexOptions(Collation,
                        maximumTokensPerDocument: 2)))))
                .Should().ThrowAsync<IndexBuildException>();
            buildFailure.Which.InnerException.Should().BeOfType<StorageResourceExhaustedException>();
            engine.Catalog.Indexes.Should().NotContain(index => index.Name == "too_small");

            var definition = await engine.CreateSpecializedIndexAsync("bounded_terms", table.Id,
                new CatalogIndexedColumn(new ColumnId(2), SortDirection.Ascending, NullSortOrder.Last),
                CatalogSpecializedIndexOptions.FullText(new CatalogFullTextIndexOptions(Collation,
                    maximumTokensPerDocument: 3, maximumTokenLength: 8)));
            var index = await engine.OpenIndexAsync(definition.Id);

            var insertFailure = await ((Func<Task>)(async () => await storage.InsertAsync(new Row([
                    SqlValue.Integer(2), SqlValue.Text("one two three four")
                ]))))
                .Should().ThrowAsync<TableMutationException>();
            insertFailure.Which.InnerException.Should().BeOfType<StorageResourceExhaustedException>();
            (await SearchAsync(index, new FullTextSearchRequest("four"))).Should().BeEmpty();

            var updateFailure = await ((Func<Task>)(async () => await storage.UpdateAsync(original, new RowUpdate([
                    new ColumnUpdate(1, SqlValue.Text("tokenlength"))
                ]))))
                .Should().ThrowAsync<TableMutationException>();
            updateFailure.Which.InnerException.Should().BeOfType<StorageResourceExhaustedException>();
            (await SearchAsync(index, new FullTextSearchRequest("one")))
                .Select(match => match.RowId).Should().Equal(original);
            var stored = await storage.GetAsync(original);
            ((TextSqlValue)stored!.Row.Values[1]).Value.Should().Be("one two three");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static async Task<CatalogTable> CreateTableWithPrimaryKeyAsync(IStorageEngine engine)
    {
        var table = await engine.CreateTableAsync("documents", [
            new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false),
            new CatalogColumn(new ColumnId(2), "body", SqlType.NVarCharMax(Collation), true)
        ]);
        await engine.CreateIndexAsync("documents_pk", table.Id, true,
            [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.First)],
            new CatalogBTreeIndexOptions(CatalogIndexStorageKind.Clustered, isPrimaryKey: true));
        return table;
    }

    private static async Task<IReadOnlyList<FullTextIndexMatch>> SearchAsync(IStorageIndex index,
        FullTextSearchRequest request, CancellationToken cancellationToken = default)
    {
        List<FullTextIndexMatch> matches = [];
        await foreach (var match in index.SearchFullTextAsync(request, cancellationToken)) matches.Add(match);
        return matches;
    }
}
