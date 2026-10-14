// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Storage;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Orchestrations.StorageMemory;
using sql_storage_engine.Services.Orchestrations.ObjectCommits;
using sql_storage_engine.Services.Orchestrations.DatabaseHeaders;

namespace sql_storage_engine.Services.Coordinations.ObjectDatabases;
internal sealed partial class ObjectDatabaseContextCoordinationService : IObjectDatabaseContextCoordinationService
{
    private readonly StorageMemoryContextOrchestrationService cache;

    private readonly ObjectCommitContextOrchestrationService remote;

    private readonly DatabaseHeaderOrchestrationService headers;

    public ObjectDatabaseContextCoordinationService(StorageMemoryContextOrchestrationService cache, ObjectCommitContextOrchestrationService remote, DatabaseHeaderOrchestrationService headers)
    {
        this.cache = cache;

        this.remote = remote;

        this.headers = headers;
    }

    internal static ObjectDatabaseContextCoordinationService CreateObjectDatabaseContextCoordinationService(StorageObjectResourceConfiguration? objectResourceConfiguration) =>
        new ObjectDatabaseContextCoordinationService(cache: StorageMemoryContextOrchestrationService.CreateStorageMemoryContextOrchestrationService(), remote: ObjectCommitContextOrchestrationService.CreateObjectCommitContextOrchestrationService(objectResourceConfiguration: objectResourceConfiguration), headers: new DatabaseHeaderOrchestrationService());

    public ValueTask<StorageStrategyContext> ProcessStorageStrategyContextAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageStrategyContext(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken);

        if (storageStrategyContext.Operation is >= 100 and <= 103)
        {
            return await cache.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }

        if (storageStrategyContext.Operation is 0 or 1)
        {
            storageStrategyContext.State ??= new StorageSessionState();

            await cache.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext with { Operation = 100 }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                await OpenAsync(storageStrategyContext: storageStrategyContext, create: storageStrategyContext.Operation == 0, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

                return storageStrategyContext;
            }
            catch
            {
                await cache.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext with { Operation = 103 }, cancellationToken: CancellationToken.None)
                    .ConfigureAwait(continueOnCapturedContext: false);

                throw;
            }
        }

        var state = storageStrategyContext.State!;

        ObjectDisposedException.ThrowIf(condition: state.Disposed, instance: state);

        if (state.Conflicted && storageStrategyContext.Operation is not (2 or 9))
        {
            throw new StorageObjectVersionConflictException(message: "This session lost remote publication ownership; reopen before further mutations.");
        }

        if (state.PendingManifest is not null && storageStrategyContext.Operation is 3 or 5 or 6 or 7)
        {
            throw new InvalidOperationException(message: "Retry the failed commit before modifying its immutable pending snapshot.");
        }

        switch (storageStrategyContext.Operation)
        {
            case 2:
                if (storageStrategyContext.Destination.Length != state.PageSize)
                {
                    throw new ArgumentException(message: "A complete page destination is required.", paramName: nameof(storageStrategyContext.Destination));
                }

                var page = await ReadPageAsync(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);

                page.CopyTo(destination: storageStrategyContext.Destination.Span);
                break;
            case 3:
                if (storageStrategyContext.PageId >= state.Header!.NextPageId.Value)
                {
                    throw new ArgumentOutOfRangeException(paramName: nameof(storageStrategyContext.PageId));
                }

                var checkedPage = await cache.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext with { Operation = 121, Bytes = storageStrategyContext.Source.ToArray() }, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);

                if (storageStrategyContext.PageId == 0)
                {
                    var changedHeader = headers.ReadDatabaseHeader(page: checkedPage.Bytes!);

                    if (changedHeader.DatabaseId != state.Header.DatabaseId || changedHeader.PageSize != state.PageSize || changedHeader.NextPageId != state.Header.NextPageId)
                    {
                        throw new StorageCorruptionException(message: "Raw page writes must preserve database identity and allocated geometry.");
                    }

                    await ReserveMutationAsync(state: state, pageIds: new ulong[] { 0 }, cancellationToken: cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext: false);

                    state.Header = changedHeader with { IsCleanShutdown = true };

                    await WriteHeaderAsync(state: state, cancellationToken: CancellationToken.None)
                        .ConfigureAwait(continueOnCapturedContext: false);

                    break;
                }

                await WritePageAsync(storageStrategyContext: storageStrategyContext, bytes: storageStrategyContext.Source.ToArray(), cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 4:
            case 8:
                await CommitAsync(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 5:
                await AllocateAsync(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 6:
                await FreeAsync(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 7:
                await PublishRootAsync(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case 9:
                state.Disposed = true;

                state.PendingManifest = null;

                state.PendingManifestBytes = null;

                state.PendingUploads = null;

                await cache.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext with { Operation = 112 }, cancellationToken: CancellationToken.None)
                    .ConfigureAwait(continueOnCapturedContext: false);
                break;
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(storageStrategyContext.Operation));
        }

        return storageStrategyContext;
    });

    private async ValueTask OpenAsync(StorageStrategyContext storageStrategyContext, bool create, CancellationToken cancellationToken)
    {
        var state = storageStrategyContext.State!;

        state.Remote = true;

        state.Name = storageStrategyContext.Name;

        state.Options = storageStrategyContext.Options;

        state.ManifestKey = state.Options!.ObjectPrefix + "/" + state.Name + "/manifest.bin";

        state.Cache = new Dictionary<int, byte[]>();

        state.Accesses = new Dictionary<int, long>();

        state.Dirty = new HashSet<int>();

        state.DirtyPages = new HashSet<ulong>();

        var read = await remote.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext with { Operation = 201, Key = state.ManifestKey, MaximumBytes = 16 * 1024 * 1024 }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (create)
        {
            if (read.Bytes is not null)
            {
                throw new StorageObjectVersionConflictException(message: "The remote database already exists.");
            }

            var identity = await remote.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext with { Operation = 205 }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            state.PageSize = storageStrategyContext.PageSize;

            state.Header = new DatabaseHeader { DatabaseId = new DatabaseId { Value = identity.Runtime }, FormatVersion = DatabaseHeaderFormat.CurrentVersion, PageSize = state.PageSize, NextPageId = new PageId { Value = 1 }, IsCleanShutdown = true };

            await WriteHeaderAsync(state: state, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        else
        {
            if (read.Bytes is null)
            {
                throw new StorageResourceException(message: "The remote database does not exist.", innerException: new System.Collections.Generic.KeyNotFoundException());
            }

            var decoded = await remote.ProcessStorageStrategyContextAsync(storageStrategyContext: read with { Operation = 211 }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            state.Manifest = decoded.Manifest;

            state.MetadataVersion = read.MetadataVersion;

            state.CommittedGeneration = state.Manifest!.Commit;

            state.PageSize = state.Manifest.PageSize;

            state.Options = state.Options with { SegmentSize = state.Manifest.SegmentSize };

            if (state.Options.SegmentSize > 16 * 1024 * 1024 || state.Options.MaximumCacheBytes < state.Options.SegmentSize * 2L)
            {
                throw new StorageResourceExhaustedException(message: "The committed segment geometry exceeds this provider's memory bounds.");
            }

            var first = await ReadSegmentAsync(state: state, index: 0, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            state.Header = headers.ReadDatabaseHeader(page: first.AsSpan(start: 0, length: state.PageSize));

            if (!state.Header.IsCleanShutdown || state.Header.PageSize != state.PageSize || state.Header.DatabaseId.Value != state.Manifest.DatabaseId || checked((long)state.Header.NextPageId.Value * state.PageSize) != state.Manifest.FileLength)
            {
                throw new StorageCorruptionException(message: "The committed header does not match its immutable object manifest.");
            }

            await ValidateFreeListAsync(state: state, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private async ValueTask<byte[]> ReadSegmentAsync(StorageSessionState state, int index, CancellationToken cancellationToken)
    {
        var found = await cache.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 110, SegmentIndex = index }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (found.Bytes is not null)
        {
            return found.Bytes;
        }

        var segments = state.Manifest?.Segments;

        var segment = segments is not null && index >= 0 && index < segments.Length ? segments[index] : null;

        byte[] bytes;

        if (segment is null)
        {
            bytes = Array.Empty<byte>();
        }
        else
        {
            var read = await remote.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 201, Key = segment.Key, MaximumBytes = segment.Length }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            if (read.Bytes is null || read.Bytes.Length != segment.Length)
            {
                throw new StorageCorruptionException(message: "An immutable database segment is missing or truncated.");
            }

            var hash = await remote.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { Operation = 204, Source = read.Bytes }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            if (!hash.Bytes!.AsSpan()
                .SequenceEqual(other: segment.Checksum))
            {
                throw new StorageCorruptionException(message: "An immutable database segment checksum does not match its manifest.");
            }

            bytes = read.Bytes;
        }

        await cache.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 111, SegmentIndex = index, Bytes = bytes }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return bytes;
    }

    private async ValueTask<byte[]> ReadPageAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken)
    {
        var state = storageStrategyContext.State!;

        if (storageStrategyContext.PageId >= state.Header!.NextPageId.Value)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(storageStrategyContext.PageId));
        }

        var offset = checked((long)storageStrategyContext.PageId * state.PageSize);

        var index = checked((int)(offset / state.Options!.SegmentSize));

        var bytes = await ReadSegmentAsync(state: state, index: index, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return bytes.AsSpan(start: (int)(offset % state.Options.SegmentSize), length: state.PageSize)
            .ToArray();
    }

    private async ValueTask ReserveMutationAsync(StorageSessionState state, ulong[] pageIds, CancellationToken cancellationToken)
    {
        var protectedIndexes = pageIds.Select(selector: page => checked((int)(checked((long)page * state.PageSize) / state.Options!.SegmentSize)))
            .Append(element: 0)
            .Distinct()
            .ToArray();

        var previousCache = new Dictionary<int, byte[]>(dictionary: state.Cache!);

        var previousAccesses = new Dictionary<int, long>(dictionary: state.Accesses!);

        var previousDirty = new HashSet<int>(collection: state.Dirty!);

        var previousBytes = state.CacheBytes;

        var previousClock = state.Clock;

        try
        {
            foreach (var index in protectedIndexes)
            {
                var bytes = await ReadSegmentAsync(state: state, index: index, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);

                var end = pageIds.Where(predicate: page => checked((long)page * state.PageSize) / state.Options!.SegmentSize == index)
                    .Select(selector: page => checked((int)((checked((long)page * state.PageSize) % state.Options!.SegmentSize) + state.PageSize)))
                    .DefaultIfEmpty(defaultValue: state.PageSize)
                    .Max();

                if (bytes.Length < end)
                {
                    var expanded = new byte[end];

                    bytes.CopyTo(array: expanded, index: 0);

                    await cache.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 111, SegmentIndex = index, Bytes = expanded }, cancellationToken: cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext: false);
                }

                state.Dirty!.Add(item: index);
            }
        }
        catch
        {
            state.Cache = previousCache;

            state.Accesses = previousAccesses;

            state.CacheBytes = previousBytes;

            state.Clock = previousClock;

            throw;
        }
        finally
        {
            state.Dirty = previousDirty;
        }
    }

    private async ValueTask WritePageAsync(StorageStrategyContext storageStrategyContext, byte[] bytes, CancellationToken cancellationToken)
    {
        var state = storageStrategyContext.State!;

        if (bytes.Length != state.PageSize)
        {
            throw new ArgumentException(message: "A complete page image is required.", paramName: nameof(bytes));
        }

        await ReserveMutationAsync(state: state, pageIds: new[] { storageStrategyContext.PageId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var offset = checked((long)storageStrategyContext.PageId * state.PageSize);

        var index = checked((int)(offset / state.Options!.SegmentSize));

        bytes.CopyTo(array: state.Cache![index], index: (int)(offset % state.Options.SegmentSize));

        state.Dirty!.Add(item: index);

        state.DirtyPages!.Add(item: storageStrategyContext.PageId);
    }

    private ValueTask WriteHeaderAsync(StorageSessionState state, CancellationToken cancellationToken)
    {
        var bytes = new byte[state.PageSize];

        headers.WriteDatabaseHeader(page: bytes, databaseHeader: state.Header! with { IsCleanShutdown = true });

        return WritePageAsync(storageStrategyContext: new StorageStrategyContext { State = state, PageId = 0 }, bytes: bytes, cancellationToken: cancellationToken);
    }

    private async ValueTask CommitAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken)
    {
        var state = storageStrategyContext.State!;

        if (state.Dirty!.Count == 0 && state.Manifest is not null)
        {
            return;
        }

        if (state.PendingManifest is null)
        {
            var entries = state.Manifest?.Segments?.ToDictionary(keySelector: item => item.Index) ?? new Dictionary<int, StorageObjectSegment>();

            var uploads = new List<(string Key, byte[] Bytes)>();

            var commit = checked(state.CommittedGeneration + 1);

            var identity = await remote.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { Operation = 205 }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            foreach (var index in state.Dirty.Order())
            {
                var expectedLength = checked((int)Math.Min(val1: state.Options!.SegmentSize, val2: checked((long)state.Header!.NextPageId.Value * state.PageSize) - checked((long)index * state.Options.SegmentSize)));

                var bytes = state.Cache![index].Length == expectedLength ? state.Cache[index] : state.Cache[index].AsSpan(start: 0, length: expectedLength)
                    .ToArray();

                var hash = await remote.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { Operation = 204, Source = bytes }, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);

                if (entries.TryGetValue(key: index, value: out var previous) && previous.Length == bytes.Length && previous.Checksum!.AsSpan()
                    .SequenceEqual(other: hash.Bytes))
                {
                    continue;
                }

                var key = state.Options!.ObjectPrefix + "/" + state.Name + "/segments/" + state.Header!.DatabaseId.Value.ToString(format: "N") + "/" + identity.Runtime.ToString(format: "N") + "/" + index.ToString(format: "D8") + ".bin";

                entries[index] = new StorageObjectSegment { Index = index, Length = bytes.Length, Key = key, Checksum = hash.Bytes };

                uploads.Add(item: (key, bytes));
            }

            var manifest = new StorageObjectManifest { DatabaseId = state.Header!.DatabaseId.Value, Commit = commit, PageSize = state.PageSize, SegmentSize = state.Options!.SegmentSize, FileLength = checked((long)state.Header.NextPageId.Value * state.PageSize), Segments = entries.Values.OrderBy(keySelector: item => item.Index)
                .ToArray() };

            var encoded = await remote.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { Operation = 210, Manifest = manifest }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            state.PendingManifest = manifest;

            state.PendingManifestBytes = encoded.Bytes;

            state.PendingUploads = uploads.ToArray();
        }

        try
        {
            await remote.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 202, Uploads = state.PendingUploads }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            var published = await remote.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 203, Key = state.ManifestKey, Bytes = state.PendingManifestBytes, MetadataVersion = state.MetadataVersion }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            AcceptCommit(state: state, version: published.MetadataVersion!);
        }
        catch (Exception exception)
        {
            state.LastError = exception.Message;

            try
            {
                var actual = await remote.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { Operation = 201, Key = state.ManifestKey, MaximumBytes = 16 * 1024 * 1024 }, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);

                if (actual.Bytes is not null && actual.Bytes.AsSpan()
                    .SequenceEqual(other: state.PendingManifestBytes))
                {
                    AcceptCommit(state: state, version: actual.MetadataVersion!);

                    return;
                }
            }
            catch (Exception reconciliationException)
            {
                state.LastError = exception.Message + "; reconciliation: " + reconciliationException.Message;
            }

            if (exception is StorageObjectVersionConflictException)
            {
                state.Conflicted = true;
            }

            throw;
        }
    }

    private static void AcceptCommit(StorageSessionState state, string version)
    {
        state.Manifest = state.PendingManifest;

        state.CommittedGeneration = state.Manifest!.Commit;

        state.MetadataVersion = version;

        state.PendingManifest = null;

        state.PendingManifestBytes = null;

        state.PendingUploads = null;

        state.Dirty!.Clear();

        state.DirtyPages!.Clear();

        state.LastError = null;
    }

    private async ValueTask ValidateFreeListAsync(StorageSessionState state, CancellationToken cancellationToken)
    {
        var current = state.Header!.FreeListHeadPageId;

        var seen = new HashSet<ulong>();

        while (current is { } page)
        {
            if (!seen.Add(item: page.Value))
            {
                throw new StorageCorruptionException(message: "The committed free list contains a cycle.");
            }

            var checkedPage = await ReadAndCheckPageAsync(state: state, pageId: page.Value, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            if (checkedPage.PageType != PageType.Free)
            {
                throw new StorageCorruptionException(message: "The committed free list references a live page.");
            }

            current = ReadNextFreePage(bytes: checkedPage.Bytes!);
        }
    }

}