using System.Net;
using System.Net.Http;
using Azure;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using sql_storage_engine;
using sql_storage_engine.AzureBlob.Exposures;
using sql_storage_engine.AzureBlob.Models;
using sql_storage_engine.Exposures.Strategies;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class AzureBlobStorageStrategyTests
{
    [Test]
    public void RegistrationMakesNoNetworkCallsAndSelectsObjectStorage()
    {
        using var handler = new StatusHandler(HttpStatusCode.Forbidden);
        using var http = new HttpClient(handler);
        var container = new BlobContainerClient(new Uri("https://offline.example/container"), new BlobClientOptions { Transport = new HttpClientTransport(http) });
        var services = new ServiceCollection();
        var builder = services.AddSqlStorageEngine();
        builder.UseBlobStorage(container).Should().BeSameAs(builder);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStorageStrategyExposure>().Should().NotBeNull();
        handler.Requests.Should().Be(0);
    }

    [TestCase(HttpStatusCode.NotFound, true)]
    [TestCase(HttpStatusCode.Forbidden, false)]
    public async Task OnlyNotFoundMetadataIsMissing(HttpStatusCode status, bool missing)
    {
        using var handler = new StatusHandler(status);
        using var http = new HttpClient(handler);
        var clientOptions = new BlobClientOptions { Transport = new HttpClientTransport(http) };
        clientOptions.Retry.MaxRetries = 0;
        var store = new AzureBlobObjectStoreExposure(new AzureBlobStorageConfiguration { ContainerSource = new BlobContainerClient(new Uri("https://offline.example/container"), clientOptions) });
        if (missing)
        {
            (await store.ReadMetadataAsync("commit.bin", 1024)).Should().BeNull();
            using var destination = new MemoryStream();
            Func<Task> download = async () => await store.DownloadAsync("missing", destination);
            await download.Should().ThrowAsync<AzureBlobStorageDependencyException>();
        }
        else
        {
            Func<Task> read = async () => await store.ReadMetadataAsync("commit.bin", 1024);
            var error = await read.Should().ThrowAsync<AzureBlobStorageDependencyException>();
            ((RequestFailedException)error.Which.InnerException!).Status.Should().Be(403);
        }
    }

    [Test]
    public async Task CancellationIsPreservedBeforeAnyNetworkRequest()
    {
        using var handler = new StatusHandler(HttpStatusCode.OK);
        using var http = new HttpClient(handler);
        var store = new AzureBlobObjectStoreExposure(new AzureBlobStorageConfiguration { ContainerSource = new BlobContainerClient(new Uri("https://offline.example/container"), new BlobClientOptions { Transport = new HttpClientTransport(http) }) });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Func<Task> read = async () => await store.ReadMetadataAsync("commit.bin", 1024, cancellation.Token);
        var error = await read.Should().ThrowAsync<OperationCanceledException>();
        error.Which.CancellationToken.Should().Be(cancellation.Token);
        handler.Requests.Should().Be(0);
    }

    [Test]
    public async Task DisposingTheProviderClosesAdmissionWithoutUsingOrDisposingTheClient()
    {
        using var handler = new StatusHandler(HttpStatusCode.NotFound);
        using var http = new HttpClient(handler);
        var options = new BlobClientOptions { Transport = new HttpClientTransport(http) };
        options.Retry.MaxRetries = 0;
        var container = new BlobContainerClient(new Uri("https://offline.example/container"), options);
        var store = new AzureBlobObjectStoreExposure(new AzureBlobStorageConfiguration { ContainerSource = container });
        await store.DisposeAsync();
        await store.DisposeAsync();
        Func<Task> closed = async () => await store.ReadMetadataAsync("commit.bin", 1024);
        await closed.Should().ThrowAsync<ObjectDisposedException>();
        handler.Requests.Should().Be(0);
        (await container.ExistsAsync()).Value.Should().BeFalse();
        handler.Requests.Should().Be(1);
    }

    [Test]
    public async Task SuccessfulAndOversizedSdkDownloadsDisposeTheirResponseStreams()
    {
        using var handler = new ContentHandler(new byte[] { 1, 2, 3 });
        using var http = new HttpClient(handler);
        var options = new BlobClientOptions { Transport = new HttpClientTransport(http) };
        options.Retry.MaxRetries = 0;
        await using var store = new AzureBlobObjectStoreExposure(new AzureBlobStorageConfiguration { ContainerSource = new BlobContainerClient(new Uri("https://offline.example/container"), options) });
        var metadata = await store.ReadMetadataAsync("commit.bin", 3);
        metadata!.Value.Bytes.Should().Equal(1, 2, 3);
        metadata.Value.Version.Should().Be("\"version1\"");
        handler.LastStream!.Disposed.Should().BeTrue();
        using var destination = new MemoryStream();
        await store.DownloadAsync("segment", destination);
        destination.ToArray().Should().Equal(1, 2, 3);
        destination.CanWrite.Should().BeTrue();
        handler.LastStream!.Disposed.Should().BeTrue();
        Func<Task> bounded = async () => await store.ReadMetadataAsync("commit.bin", 2);
        await bounded.Should().ThrowAsync<AzureBlobMetadataFormatException>();
        handler.LastStream!.Disposed.Should().BeTrue();
    }

    [Test, Category("Integration")]
    public async Task AzuriteStreamsImmutableSegmentsWithCallerOwnedStreams()
    {
        var container = await CreateContainerAsync();
        try
        {
            var store = new AzureBlobObjectStoreExposure(new AzureBlobStorageConfiguration { ContainerSource = container, TransferSize = 65536, UploadConcurrency = 3 });
            var expected = new byte[4 * 65536 + 17];
            new Random(42).NextBytes(expected);
            using var source = new NonSeekableReadStream(expected);
            await store.UploadAsync("segments/one", source);
            source.Disposed.Should().BeFalse();
            source.LargestRead.Should().BeLessThanOrEqualTo(65536);
            using var destination = new MemoryStream();
            await store.DownloadAsync("segments/one", destination);
            destination.ToArray().Should().Equal(expected);
            destination.CanWrite.Should().BeTrue();
            using var overwrite = new MemoryStream(new byte[] { 9 });
            Func<Task> upload = async () => await store.UploadAsync("segments/one", overwrite);
            var failure = await upload.Should().ThrowAsync<StorageObjectVersionConflictException>();
            var cause = (RequestFailedException)failure.Which.InnerException!;
            cause.Status.Should().BeOneOf(409, 412);
            if (cause.Status == 409) cause.ErrorCode.Should().Be("BlobAlreadyExists");
            destination.SetLength(0);
            await store.DownloadAsync("segments/one", destination);
            destination.ToArray().Should().Equal(expected);
        }
        finally { await container.DeleteIfExistsAsync(); }
    }

    [Test, Category("Integration")]
    public async Task AzuriteMetadataUsesCreateAndEtagCompareAndSwapWithBoundedReads()
    {
        var container = await CreateContainerAsync();
        try
        {
            var store = new AzureBlobObjectStoreExposure(new AzureBlobStorageConfiguration { ContainerSource = container });
            (await store.ReadMetadataAsync("commit.bin", 64)).Should().BeNull();
            var initial = await store.PublishMetadataAsync("commit.bin", new byte[] { 1, 2, 3 }, null);
            var read = await store.ReadMetadataAsync("commit.bin", 64);
            read.HasValue.Should().BeTrue();
            read!.Value.Version.Should().Be(initial);
            read!.Value.Bytes.Should().Equal(1, 2, 3);
            Func<Task> duplicate = async () => await store.PublishMetadataAsync("commit.bin", new byte[] { 4 }, null);
            await duplicate.Should().ThrowAsync<StorageObjectVersionConflictException>();
            var updated = await store.PublishMetadataAsync("commit.bin", new byte[] { 5, 6, 7, 8 }, initial);
            updated.Should().NotBe(initial);
            Func<Task> stale = async () => await store.PublishMetadataAsync("commit.bin", new byte[] { 9 }, initial);
            var staleFailure = await stale.Should().ThrowAsync<StorageObjectVersionConflictException>();
            ((RequestFailedException)staleFailure.Which.InnerException!).Status.Should().Be(412);
            Func<Task> bounded = async () => await store.ReadMetadataAsync("commit.bin", 3);
            await bounded.Should().ThrowAsync<AzureBlobMetadataFormatException>();
            var current = await store.ReadMetadataAsync("commit.bin", 4);
            current!.Value.Version.Should().Be(updated);
            current!.Value.Bytes.Should().Equal(5, 6, 7, 8);
        }
        finally { await container.DeleteIfExistsAsync(); }
    }

    [Test, Category("Integration")]
    public async Task AzuriteDiStrategyCommitsAndReopensOpaqueHeapWithoutALocalDirectory()
    {
        var container = await CreateContainerAsync();
        try
        {
            var services = new ServiceCollection();
            services.AddSqlStorageEngine().UseBlobStorage(container, new StorageStrategyOptions { ObjectPrefix = "physical", SegmentSize = 65536, UploadConcurrency = 2, MaximumCacheBytes = 4 * 1024 * 1024 });
            using var provider = services.BuildServiceProvider();
            var strategy = provider.GetRequiredService<IStorageStrategyExposure>();
            var payload = "Opaque heap bytes stored through Azure Blob"u8.ToArray();
            sql_storage_engine.Models.Identifiers.RowId rowId;
            await using (var database = await strategy.CreateStorageDatabaseExposureAsync("heap", 4096))
            await using (var pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(database, 4, leaveOpen: true))
            {
                var heap = await sql_storage_engine.Exposures.Heap.TableHeapExposure.CreateTableHeapExposureAsync(pool, database);
                rowId = await heap.InsertAsync(payload);
                await pool.FlushAllAsync();
                await database.PublishCatalogRootAsync(heap.ReadRootPageId());
                await database.CommitAsync();
                database.ReadCommitStatus().CommittedGeneration.Should().BeGreaterThan(0);
                database.ReadCommitStatus().PendingPages.Should().Be(0);
            }
            var reopenedServices = new ServiceCollection();
            reopenedServices.AddSqlStorageEngine().UseBlobStorage(container, new StorageStrategyOptions { ObjectPrefix = "physical", SegmentSize = 65536, MaximumCacheBytes = 4 * 1024 * 1024 });
            using var reopenedProvider = reopenedServices.BuildServiceProvider();
            await using (var reopened = await reopenedProvider.GetRequiredService<IStorageStrategyExposure>().OpenAsync("heap"))
            await using (var pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(reopened, 4, leaveOpen: true))
            {
                var heap = await sql_storage_engine.Exposures.Heap.TableHeapExposure.OpenAsync(reopened.ReadHeader().CatalogRootPageId!.Value, pool, reopened);
                var row = await heap.ReadAsync(rowId);
                row.Row.ToArray().Should().Equal(payload);
            }
        }
        finally { await container.DeleteIfExistsAsync(); }
    }

    private static async Task<BlobContainerClient> CreateContainerAsync()
    {
        if (Environment.GetEnvironmentVariable("SQL_STORAGE_AZURITE") != "1") Assert.Ignore("Set SQL_STORAGE_AZURITE=1 to run against the local Azure Blob emulator.");
        var options = new BlobClientOptions();
        options.Retry.MaxRetries = 0;
        var container = new BlobContainerClient("UseDevelopmentStorage=true", "sql-storage-azurite-" + Guid.NewGuid().ToString("N"), options);
        await container.CreateIfNotExistsAsync();
        return container;
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var errorCode = status == HttpStatusCode.NotFound ? (request.RequestUri!.Query.Contains("restype=container", StringComparison.Ordinal) ? "ContainerNotFound" : "BlobNotFound") : "AuthorizationFailure";
            var response = new HttpResponseMessage(status) { Content = new StringContent($"<Error><Code>{errorCode}</Code><Message>Fake Azure response.</Message></Error>", System.Text.Encoding.UTF8, "application/xml"), RequestMessage = request };
            response.Headers.Add("x-ms-error-code", errorCode);
            response.Headers.Add("x-ms-request-id", Guid.NewGuid().ToString("N"));
            return Task.FromResult(response);
        }
    }

    private sealed class ContentHandler(byte[] bytes) : HttpMessageHandler
    {
        public TrackingStream? LastStream { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastStream = new TrackingStream(bytes);
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(LastStream), RequestMessage = request };
            response.Content.Headers.ContentLength = bytes.Length;
            response.Content.Headers.LastModified = DateTimeOffset.UtcNow;
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"version1\"");
            response.Headers.Add("x-ms-blob-type", "BlockBlob");
            response.Headers.Add("x-ms-request-id", Guid.NewGuid().ToString("N"));
            return Task.FromResult(response);
        }
    }

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class NonSeekableReadStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream source = new(bytes);
        public bool Disposed { get; private set; }
        public int LargestRead { get; private set; }
        public override bool CanRead => !Disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            LargestRead = Math.Max(LargestRead, count);
            return source.Read(buffer, offset, count);
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            LargestRead = Math.Max(LargestRead, buffer.Length);
            return source.ReadAsync(buffer, cancellationToken);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            if (disposing) source.Dispose();
            base.Dispose(disposing);
        }
    }
}