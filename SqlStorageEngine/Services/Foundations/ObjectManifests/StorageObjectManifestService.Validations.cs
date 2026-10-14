// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.ObjectManifests;
internal sealed partial class StorageObjectManifestService
{
    private const int MaximumBytes = 16 * 1024 * 1024;
    private const int MaximumSegments = 65536;
    private const int MaximumKeyBytes = 1024;
    private static void ValidateStorageStrategyContextEncode(StorageStrategyContext storageStrategyContext)
    {
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext);
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext.Manifest);
        ValidateStorageObjectManifest(storageObjectManifest: storageStrategyContext.Manifest);
    }

    private static void ValidateStorageStrategyContextDecode(StorageStrategyContext storageStrategyContext)
    {
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext);
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext.Bytes);
    }

    private static void ValidateStorageObjectManifest(StorageObjectManifest storageObjectManifest)
    {
        if (storageObjectManifest.DatabaseId == Guid.Empty || storageObjectManifest.Commit < 1 || !Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)storageObjectManifest.PageSize) || storageObjectManifest.SegmentSize < storageObjectManifest.PageSize || storageObjectManifest.SegmentSize > 64 * 1024 * 1024 || storageObjectManifest.SegmentSize % storageObjectManifest.PageSize != 0 || storageObjectManifest.FileLength < storageObjectManifest.PageSize || storageObjectManifest.FileLength % storageObjectManifest.PageSize != 0 || storageObjectManifest.Segments is null || storageObjectManifest.Segments.Length is 0 or > MaximumSegments)
        {
            throw new StorageFormatException(message: "Object storage manifest geometry or identity is invalid.");
        }

        var expectedCount = (storageObjectManifest.FileLength - 1) / storageObjectManifest.SegmentSize + 1;

        if (expectedCount != storageObjectManifest.Segments.Length)
        {
            throw new StorageFormatException(message: "Object storage manifest segment count is invalid.");
        }

        for (var index = 0; index < storageObjectManifest.Segments.Length; index++)
        {
            var segment = storageObjectManifest.Segments[index];
            var expectedLength = Math.Min(val1: storageObjectManifest.SegmentSize, val2: storageObjectManifest.FileLength - (long)index * storageObjectManifest.SegmentSize);

            if (segment is null || segment.Index != index || segment.Length != expectedLength || string.IsNullOrWhiteSpace(value: segment.Key) || segment.Key.Length > MaximumKeyBytes || segment.Checksum is null || segment.Checksum.Length != 32)
            {
                throw new StorageFormatException(message: "Object storage manifest segment is invalid.");
            }
        }
    }
}