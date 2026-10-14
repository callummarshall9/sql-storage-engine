using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Exposures.Strategies;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class FileStorageStrategyTests
{
    [Test]
    public async Task RegistrationCreatesCommitsAndReopensARealPhysicalDatabase()
    {
        var directory = Path.Combine(Path.GetTempPath(), "file-strategy-" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new ServiceCollection();
            services.AddSqlStorageEngine().UseLocalFileSystem(directory);
            using var container = services.BuildServiceProvider();
            var strategy = container.GetRequiredService<IStorageStrategyExposure>();
            sql_storage_engine.Models.Identifiers.PageId pageId;
            sql_storage_engine.Models.Identifiers.DatabaseId identity;
            await using (var database = await strategy.CreateStorageDatabaseExposureAsync("example"))
            {
                identity = database.ReadHeader().DatabaseId;
                pageId = await database.AllocateAsync(PageType.Heap);
                var page = new byte[database.GetPageSize()];
                await database.ReadAsync(pageId, page);
                page[^1] = 123;
                PageChecksumExposure.WriteChecksum(page, page.Length);
                await database.WriteAsync(pageId, page);
                database.ReadCommitStatus().PendingPages.Should().BeGreaterThan(0);
                await database.CommitAsync();
                database.ReadCommitStatus().PendingPages.Should().Be(0);
                database.ReadCommitStatus().CommittedGeneration.Should().Be(1);
                await database.FlushAsync();
                database.ReadCommitStatus().CommittedGeneration.Should().Be(1);
            }
            File.Exists(Path.Combine(directory, "example.db")).Should().BeTrue();
            await using var reopened = await strategy.OpenAsync("example");
            reopened.ReadHeader().DatabaseId.Should().Be(identity);
            var stored = new byte[reopened.GetPageSize()];
            await reopened.ReadAsync(pageId, stored);
            stored[^1].Should().Be(123);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestCase("../escape")]
    [TestCase("nested/name")]
    [TestCase(".")]
    [TestCase("")]
    public async Task UnsafeNamesAreRejectedBeforeAnyFileIsCreated(string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), "file-strategy-" + Guid.NewGuid().ToString("N"));
        IStorageStrategyExposure strategy = new FileStorageStrategyExposure(new StorageStrategyOptions { Directory = directory });
        await ((Func<Task>)(async () => await strategy.CreateStorageDatabaseExposureAsync(name))).Should().ThrowAsync<ArgumentException>();
        Directory.Exists(directory).Should().BeFalse();
    }

    [Test]
    public async Task CancellationAndRepeatedDisposalPreserveTheResourceLifetime()
    {
        var directory = Path.Combine(Path.GetTempPath(), "file-strategy-" + Guid.NewGuid().ToString("N"));
        try
        {
            IStorageStrategyExposure strategy = new FileStorageStrategyExposure(new StorageStrategyOptions { Directory = directory });
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var cancelled = await ((Func<Task>)(async () => await strategy.CreateStorageDatabaseExposureAsync("cancelled", cancellationToken: cancellation.Token))).Should().ThrowAsync<OperationCanceledException>();
            cancelled.Which.CancellationToken.Should().Be(cancellation.Token);
            Directory.Exists(directory).Should().BeFalse();
            var database = await strategy.CreateStorageDatabaseExposureAsync("example");
            await database.DisposeAsync();
            await database.DisposeAsync();
            await ((Func<Task>)(async () => await database.CommitAsync())).Should().ThrowAsync<ObjectDisposedException>();
            await using var reopened = await strategy.OpenAsync("example");
            reopened.GetPageSize().Should().Be(PageConstants.DefaultSize);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}