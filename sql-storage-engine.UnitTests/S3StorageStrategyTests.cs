using System.Net;
using Microsoft.Extensions.DependencyInjection;
using sql_storage_engine.Exposures.Strategies;
using sql_storage_engine.Pages;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using AwesomeAssertions;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.S3.Exposures.Strategies;
using sql_storage_engine.S3.Models.Exceptions;

namespace sql_storage_engine.UnitTests;
public sealed class S3StorageStrategyTests
{
    [Test]
    public async Task UseS3WithoutDirectoryCommitsAndReopensThroughTheRemoteObjects()
    {
        using var client = new FakeS3();
        var services = new ServiceCollection();
        services.AddSqlStorageEngine().UseS3(client, "bucket");
        using var container = services.BuildServiceProvider();
        var strategy = container.GetRequiredService<IStorageStrategyExposure>();
        sql_storage_engine.Models.Identifiers.PageId pageId;
        sql_storage_engine.Models.Identifiers.DatabaseId databaseId;
        await using (var database = await strategy.CreateStorageDatabaseExposureAsync("remote"))
        {
            databaseId = database.ReadHeader().DatabaseId;
            pageId = await database.AllocateAsync(PageType.Heap);
            await database.CommitAsync();
        }
        await using var reopened = await strategy.OpenAsync("remote");
        reopened.ReadHeader().DatabaseId.Should().Be(databaseId);
        var page = new byte[reopened.GetPageSize()];
        await reopened.ReadAsync(pageId, page);
        client.Puts.Should().NotBeEmpty();
        client.Puts.Should().OnlyContain(request => request.IfNoneMatch == "*" || request.IfMatch != null);
    }

    [Test]
    public async Task SegmentUploadPassesTheSourceStreamAndPreventsReplacement()
    {
        using var client = new FakeS3();
        var provider = new S3StorageObjectStoreExposure(client, "bucket");
        using var source = new TrackingStream(new byte[4 * 1024 * 1024]);
        await provider.UploadAsync("segment", source);
        client.Puts.Single().InputStream.Should().BeSameAs(source);
        client.Puts.Single().IfNoneMatch.Should().Be("*");
        client.Puts.Single().IfMatch.Should().BeNull();
        client.Puts.Single().AutoCloseStream.Should().BeFalse();
        client.Puts.Single().AutoResetStreamPosition.Should().BeFalse();
        source.Closed.Should().BeFalse();
        client.Closed.Should().BeFalse();
        source.Position = 0;
        await ((Func<Task>)(async () => await provider.UploadAsync("segment", source))).Should().ThrowAsync<StorageObjectVersionConflictException>();
        client.Objects["segment"].Bytes.Length.Should().Be(4 * 1024 * 1024);
    }

    [Test]
    public async Task PublicationCreatesConditionallyAndUsesTheExactETagForCompareAndSwap()
    {
        using var client = new FakeS3();
        var provider = new S3StorageObjectStoreExposure(client, "bucket");
        var first = await provider.PublishMetadataAsync("manifest", [1], null);
        client.Puts[0].IfNoneMatch.Should().Be("*");
        client.Puts[0].IfMatch.Should().BeNull();
        var second = await provider.PublishMetadataAsync("manifest", [2], first);
        client.Puts[1].IfMatch.Should().Be(first);
        client.Puts[1].IfNoneMatch.Should().BeNull();
        second.Should().NotBe(first);
        await ((Func<Task>)(async () => await provider.PublishMetadataAsync("manifest", [3], first))).Should().ThrowAsync<StorageObjectVersionConflictException>();
        await ((Func<Task>)(async () => await provider.PublishMetadataAsync("manifest", [4], null))).Should().ThrowAsync<StorageObjectVersionConflictException>();
        client.Objects["manifest"].Bytes.Should().Equal(2);
        var read = await provider.ReadMetadataAsync("manifest", 16);
        read!.Value.Version.Should().Be(second);
        read.Value.Bytes.Should().Equal(2);
    }

    [TestCase(HttpStatusCode.PreconditionFailed)]
    [TestCase(HttpStatusCode.Conflict)]
    public async Task BothConditionalConflictResponsesRemainVisible(HttpStatusCode status)
    {
        using var client = new FakeS3 { PutError = Failure(status) };
        var provider = new S3StorageObjectStoreExposure(client, "bucket");
        var error = await ((Func<Task>)(async () => await provider.PublishMetadataAsync("manifest", [1], "old"))).Should().ThrowAsync<StorageObjectVersionConflictException>();
        error.Which.InnerException.Should().BeSameAs(client.PutError);
    }

    [Test]
    public async Task MetadataMissingIsNullOnlyForNotFound()
    {
        using var client = new FakeS3();
        var provider = new S3StorageObjectStoreExposure(client, "bucket");
        (await provider.ReadMetadataAsync("absent", 16)).Should().BeNull();
        client.GetError = Failure(HttpStatusCode.Forbidden);
        var failure = await ((Func<Task>)(async () => await provider.ReadMetadataAsync("absent", 16))).Should().ThrowAsync<S3ObjectStoreDependencyException>();
        failure.Which.InnerException.Should().BeSameAs(client.GetError);
    }

    [TestCase(17, 16)]
    [TestCase(-1, 16)]
    public async Task MetadataBoundsRejectBeforeReadingAndDisposeTheResponse(long declaredLength, int maximum)
    {
        using var client = new FakeS3();
        var stream = new TrackingStream([1, 2, 3]);
        client.Response = () => new GetObjectResponse { ContentLength = declaredLength, ETag = "etag", ResponseStream = stream };
        var provider = new S3StorageObjectStoreExposure(client, "bucket");
        await ((Func<Task>)(async () => await provider.ReadMetadataAsync("manifest", maximum))).Should().ThrowAsync<S3ObjectStoreDependencyException>();
        stream.Reads.Should().Be(0);
        stream.Closed.Should().BeTrue();
        client.Closed.Should().BeFalse();
    }

    [TestCase(2)]
    [TestCase(4)]
    public async Task MetadataRejectsTrailingAndTruncatedBodies(long declaredLength)
    {
        using var client = new FakeS3();
        var stream = new TrackingStream([1, 2, 3]);
        client.Response = () => new GetObjectResponse { ContentLength = declaredLength, ETag = "etag", ResponseStream = stream };
        await ((Func<Task>)(async () => await new S3StorageObjectStoreExposure(client, "bucket").ReadMetadataAsync("manifest", 16))).Should().ThrowAsync<S3ObjectStoreDependencyException>();
        stream.Closed.Should().BeTrue();
    }

    [Test]
    public async Task DownloadStreamsAndDisposesTheResponseWithoutClosingTheDestination()
    {
        using var client = new FakeS3();
        var source = new TrackingStream(new byte[4 * 1024 * 1024]);
        client.Response = () => new GetObjectResponse { ContentLength = source.Length, ETag = "etag", ResponseStream = source };
        using var destination = new MemoryStream();
        await new S3StorageObjectStoreExposure(client, "bucket").DownloadAsync("segment", destination);
        destination.Length.Should().Be(4 * 1024 * 1024);
        destination.CanWrite.Should().BeTrue();
        source.Closed.Should().BeTrue();
        client.Closed.Should().BeFalse();
    }

    [Test]
    public async Task CancelledCallsPreserveTheTokenAndDoNotInvokeTheSdk()
    {
        using var client = new FakeS3();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var provider = new S3StorageObjectStoreExposure(client, "bucket");
        using var destination = new MemoryStream();
        var failure = await ((Func<Task>)(async () => await provider.DownloadAsync("segment", destination, cancellation.Token))).Should().ThrowAsync<OperationCanceledException>();
        failure.Which.CancellationToken.Should().Be(cancellation.Token);
        client.Gets.Should().Be(0);
    }

    private static AmazonS3Exception Failure(HttpStatusCode status) => new("S3 failure") { StatusCode = status };

    private sealed class FakeS3 : AmazonS3Client
    {
        public Dictionary<string, (byte[] Bytes, string ETag)> Objects { get; } = [];
        public List<PutObjectRequest> Puts { get; } = [];
        public Func<GetObjectResponse>? Response { get; set; }
        public AmazonS3Exception? GetError { get; set; }
        public AmazonS3Exception? PutError { get; set; }
        public int Gets { get; private set; }
        public bool Closed { get; private set; }
        public FakeS3() : base(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "http://localhost", ForcePathStyle = true }) { }
        public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken cancellationToken = default)
        {
            Gets++;
            cancellationToken.ThrowIfCancellationRequested();
            if (GetError is not null) throw GetError;
            if (Response is not null) return Task.FromResult(Response());
            if (!Objects.TryGetValue(request.Key, out var value)) throw Failure(HttpStatusCode.NotFound);
            return Task.FromResult(new GetObjectResponse { ContentLength = value.Bytes.Length, ETag = value.ETag, ResponseStream = new MemoryStream(value.Bytes) });
        }
        public override async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
        {
            Puts.Add(request);
            if (PutError is not null) throw PutError;
            var exists = Objects.TryGetValue(request.Key, out var existing);
            if ((request.IfNoneMatch == "*" && exists) || (request.IfMatch != null && (!exists || request.IfMatch != existing.ETag))) throw Failure(HttpStatusCode.PreconditionFailed);
            using var bytes = new MemoryStream();
            await request.InputStream.CopyToAsync(bytes, cancellationToken);
            var version = $"\"{Puts.Count}\"";
            Objects[request.Key] = (bytes.ToArray(), version);
            return new PutObjectResponse { ETag = version };
        }
        protected override void Dispose(bool disposing)
        {
            Closed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int Reads { get; private set; }
        public bool Closed { get; private set; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Reads++;
            return base.ReadAsync(buffer, cancellationToken);
        }
        protected override void Dispose(bool disposing)
        {
            Closed = true;
            base.Dispose(disposing);
        }
    }
}