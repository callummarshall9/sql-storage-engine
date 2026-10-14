// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Concurrent;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Strategies;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Exposures.Buffers;
using sql_storage_engine.Exposures.Heap;
using sql_storage_engine.Exposures.Indexes;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class ObjectStorageStrategyTests
{
    [Test]
    public async Task RemoteCommit_WaitsForUploadAndReopensPhysicalHeapAndIndex()
    {
        var store = new Objects();
        var strategy = Strategy(store);
        RowId row;
        PageId heapRoot;
        PageId indexRoot;
        await using (var database = await strategy.CreateStorageDatabaseExposureAsync("records"))
        {
            await using (var pool = new BufferPoolExposure(database, 3, leaveOpen: true))
            {
                var heap = await TableHeapExposure.CreateTableHeapExposureAsync(pool, database);
                heapRoot = heap.ReadRootPageId();
                row = await heap.InsertAsync(new byte[] { 0, 255, 7 });
                var root = new IndexRootReferenceExposure(default);
                var tree = await PersistentBPlusTreeExposure.CreatePersistentBPlusTreeExposureAsync(pool, database, root);
                await tree.InsertAsync(IndexKeyExposure.CreateIndexKey(new byte[] { 1 }), row);
                indexRoot = tree.ReadRootPageId();
                await database.PublishCatalogRootAsync(heapRoot);
                store.BlockUploads = true;
                var flush = pool.FlushAllAsync().AsTask();
                await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                flush.IsCompleted.Should().BeFalse();
                store.Release.TrySetResult();
                await flush;
            }

            database.ReadCommitStatus().CommittedGeneration.Should().Be(1);
            database.ReadCommitStatus().PendingPages.Should().Be(0);
            store.UploadLengths.Should().HaveCount(2);
            store.UploadLengths.Sum().Should().Be(3 * database.GetPageSize());
        }

        await using var reopened = await Strategy(store).OpenAsync("records");
        reopened.ReadHeader().IsCleanShutdown.Should().BeTrue();
        reopened.ReadHeader().CatalogRootPageId.Should().Be(heapRoot);
        await using var readPool = new BufferPoolExposure(reopened, 2, leaveOpen: true);
        var readHeap = await TableHeapExposure.OpenAsync(heapRoot, readPool, reopened);
        (await readHeap.ReadAsync(row)).Row.ToArray().Should().Equal(0, 255, 7);
        var readTree = new PersistentBPlusTreeExposure(readPool, reopened, new IndexRootReferenceExposure(indexRoot));
        (await readTree.FindAsync(IndexKeyExposure.CreateIndexKey(new byte[] { 1 }))).Should().Equal(row);
    }

    [Test]
    public async Task PartialUploadAndLostResponses_AreRetryableWithoutChangingImmutableSnapshot()
    {
        var store = new Objects();
        await using var database = await Strategy(store).CreateStorageDatabaseExposureAsync("retry");
        await database.CommitAsync();
        for (var index = 0; index < 5; index++)
            await database.AllocateAsync(PageType.Heap);
        store.FailUpload = store.UploadCalls + 2;
        await ((Func<Task>)(async () => await database.CommitAsync())).Should().ThrowAsync<StorageException>();
        database.ReadCommitStatus().LastError.Should().NotBeNull();
        database.ReadCommitStatus().CommittedGeneration.Should().Be(1);
        await database.CommitAsync();
        database.ReadCommitStatus().CommittedGeneration.Should().Be(2);
        database.ReadCommitStatus().LastError.Should().BeNull();
        store.LoseUploadResponse = true;
        await database.AllocateAsync(PageType.Heap);
        await database.CommitAsync();
        store.LosePublishResponse = true;
        await database.AllocateAsync(PageType.Heap);
        await database.CommitAsync();
        database.ReadCommitStatus().CommittedGeneration.Should().Be(4);
        await using var reopened = await Strategy(store).OpenAsync("retry");
        reopened.ReadHeader().NextPageId.Should().Be(database.ReadHeader().NextPageId);
    }

    [Test]
    public async Task CompetingWriterFailsClosedAndCannotOverwriteCommittedHead()
    {
        var store = new Objects();
        await using (var initial = await Strategy(store).CreateStorageDatabaseExposureAsync("writers"))
            await initial.CommitAsync();
        await using var first = await Strategy(store).OpenAsync("writers");
        await using var second = await Strategy(store).OpenAsync("writers");
        await first.AllocateAsync(PageType.Heap);
        await second.AllocateAsync(PageType.Catalog);
        await first.CommitAsync();
        await ((Func<Task>)(async () => await second.CommitAsync())).Should().ThrowAsync<StorageObjectVersionConflictException>();
        await ((Func<Task>)(async () => await second.AllocateAsync(PageType.Heap))).Should().ThrowAsync<StorageObjectVersionConflictException>();
        await using var reopened = await Strategy(store).OpenAsync("writers");
        var page = new byte[reopened.GetPageSize()];
        await reopened.ReadAsync(new PageId { Value = 1 }, page);
        PageHeaderExposure.ReadPageHeader(page).PageType.Should().Be(PageType.Heap);
    }

    [Test]
    public async Task DisposeDiscardsUncommittedPagesAndNoLocalDirectoryIsNeeded()
    {
        var store = new Objects();
        await using (var database = await Strategy(store).CreateStorageDatabaseExposureAsync("volatile"))
        {
            await database.CommitAsync();
            await database.AllocateAsync(PageType.Heap);
        }

        await using var reopened = await Strategy(store).OpenAsync("volatile");
        reopened.ReadHeader().NextPageId.Value.Should().Be(1);
    }

    [Test]
    public async Task DirtyCacheOverflowLeavesAllocationHeaderAndCommittedGeometryUnchanged()
    {
        var store = new Objects();
        var options = new StorageStrategyOptions { SegmentSize = 16384, MaximumCacheBytes = 32768, UploadConcurrency = 1 };
        await using var database = await ((IStorageStrategyExposure)new ObjectStorageStrategyExposure(options, store)).CreateStorageDatabaseExposureAsync("bounded");
        for (var index = 0; index < 3; index++)
            await database.AllocateAsync(PageType.Heap);
        var before = database.ReadHeader();
        await ((Func<Task>)(async () => await database.AllocateAsync(PageType.Heap))).Should().ThrowAsync<StorageResourceExhaustedException>();
        database.ReadHeader().Should().Be(before);
        await database.CommitAsync();
        await using var reopened = await ((IStorageStrategyExposure)new ObjectStorageStrategyExposure(options, store)).OpenAsync("bounded");
        reopened.ReadHeader().NextPageId.Should().Be(before.NextPageId);
    }

    [Test]
    public async Task SegmentTamperingIsRejectedBeforePagesAreExposed()
    {
        var store = new Objects();
        await using (var database = await Strategy(store).CreateStorageDatabaseExposureAsync("corrupt"))
            await database.CommitAsync();
        store.CorruptSegment();
        await ((Func<Task>)(async () => await Strategy(store).OpenAsync("corrupt"))).Should().ThrowAsync<StorageCorruptionException>();
    }

    private static IStorageStrategyExposure Strategy(Objects store) =>
        new ObjectStorageStrategyExposure(new StorageStrategyOptions { SegmentSize = 16384, MaximumCacheBytes = 131072, UploadConcurrency = 2 }, store);

    private sealed class Objects : IStorageObjectStoreExposure
    {
        private readonly object sync = new();
        private readonly Dictionary<string, (byte[] Bytes, string Version)> objects = new();
        private int version;
        public int UploadCalls;
        public int FailUpload;
        public bool LoseUploadResponse;
        public bool LosePublishResponse;
        public bool BlockUploads;
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentBag<int> UploadLengths = new();

        public ValueTask<(byte[] Bytes, string Version)?> ReadMetadataAsync(string key, int maximumBytes, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync)
            {
                if (!objects.TryGetValue(key, out var value))
                    return ValueTask.FromResult<(byte[] Bytes, string Version)?>(null);
                if (value.Bytes.Length > maximumBytes)
                    throw new StorageFormatException("Object is too large.");
                return ValueTask.FromResult<(byte[] Bytes, string Version)?>((value.Bytes.ToArray(), value.Version));
            }
        }

        public async ValueTask DownloadAsync(string key, Stream destination, CancellationToken cancellationToken = default)
        {
            var value = await ReadMetadataAsync(key, 16 * 1024 * 1024, cancellationToken);
            await destination.WriteAsync(value!.Value.Bytes, cancellationToken);
        }

        public async ValueTask UploadAsync(string key, Stream source, CancellationToken cancellationToken = default)
        {
            var ordinal = Interlocked.Increment(ref UploadCalls);
            if (BlockUploads)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            if (ordinal == FailUpload)
                throw new IOException("Injected upload failure.");
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();
            lock (sync)
            {
                if (objects.ContainsKey(key))
                    throw new StorageObjectVersionConflictException("Immutable key already exists.");
                objects.Add(key, (bytes, (++version).ToString()));
                UploadLengths.Add(bytes.Length);
                if (LoseUploadResponse)
                {
                    LoseUploadResponse = false;
                    throw new IOException("Upload response lost after storing bytes.");
                }
            }
        }

        public ValueTask<string> PublishMetadataAsync(string key, byte[] bytes, string? expectedVersion, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync)
            {
                if (objects.TryGetValue(key, out var previous) ? previous.Version != expectedVersion : expectedVersion is not null)
                    throw new StorageObjectVersionConflictException("Manifest version changed.");
                var current = (++version).ToString();
                objects[key] = (bytes.ToArray(), current);
                if (LosePublishResponse)
                {
                    LosePublishResponse = false;
                    throw new IOException("Publication response lost after accepting manifest.");
                }
                return ValueTask.FromResult(current);
            }
        }

        public void CorruptSegment()
        {
            lock (sync)
            {
                var key = objects.Keys.Single(key => key.Contains("/segments/"));
                objects[key].Bytes[0] ^= 1;
            }
        }
    }
}
