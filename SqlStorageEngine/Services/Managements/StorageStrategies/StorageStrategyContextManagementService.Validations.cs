// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Strategies;

namespace sql_storage_engine.Services.Managements.StorageStrategies;
internal sealed partial class StorageStrategyContextManagementService
{
    private static void ValidateConfiguration(StorageStrategyContext storageStrategyContext)
    {
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext);
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext.Options);

        if (storageStrategyContext.Objects is null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(argument: storageStrategyContext.Options.Directory);
        }

        if (storageStrategyContext.Objects is not null)
        {
            ArgumentNullException.ThrowIfNull(argument: storageStrategyContext.Objects.Source);
            ArgumentNullException.ThrowIfNull(argument: storageStrategyContext.Objects.Read);
            ArgumentNullException.ThrowIfNull(argument: storageStrategyContext.Objects.Upload);
            ArgumentNullException.ThrowIfNull(argument: storageStrategyContext.Objects.Publish);
        }

        var options = NormalizeOptions(storageStrategyOptions: storageStrategyContext.Options);

        if (options.SegmentSize is < 4096 or > 16 * 1024 * 1024 || options.UploadConcurrency is < 1 or > 64 || options.MaximumCacheBytes < 2L * options.SegmentSize || string.IsNullOrWhiteSpace(value: options.ObjectPrefix))
        {
            throw new ArgumentException(message: "Storage strategy limits or prefix are invalid.");
        }
    }

    private static StorageStrategyOptions NormalizeOptions(StorageStrategyOptions storageStrategyOptions) =>
        storageStrategyOptions with { SegmentSize = storageStrategyOptions.SegmentSize == 0 ? 4 * 1024 * 1024 : storageStrategyOptions.SegmentSize, UploadConcurrency = storageStrategyOptions.UploadConcurrency == 0 ? 4 : storageStrategyOptions.UploadConcurrency, MaximumCacheBytes = storageStrategyOptions.MaximumCacheBytes == 0 ? 128L * 1024 * 1024 : storageStrategyOptions.MaximumCacheBytes, ObjectPrefix = storageStrategyOptions.ObjectPrefix ?? "databases" };

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: name);

        if (name.Length > 128)
        {
            throw new ArgumentException(message: "The storage database name is too long.");
        }

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(c: character) && character is not ('-' or '_'))
            {
                throw new ArgumentException(message: "The storage database name contains invalid characters.");
            }
        }
    }

    private void ValidateRequest(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext);
        cancellationToken.ThrowIfCancellationRequested();

        if (storageStrategyContext.Operation is < 0 or > 9)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(storageStrategyContext.Operation));
        }

        if (storageStrategyContext.Operation is 0 or 1)
        {
            ValidateName(name: storageStrategyContext.Name!);

            if (storageStrategyContext.Operation == 0 && !Enum.IsDefined(value: (PageSize)storageStrategyContext.PageSize))
            {
                throw new ArgumentOutOfRangeException(paramName: nameof(storageStrategyContext.PageSize));
            }

            if (remote && storageStrategyContext.Operation == 0 && options.SegmentSize % storageStrategyContext.PageSize != 0)
            {
                throw new ArgumentException(message: "The object segment size must contain complete pages.");
            }

            return;
        }

        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext.State);

        if (storageStrategyContext.State.Disposed && storageStrategyContext.Operation != 9)
        {
            throw new ObjectDisposedException(objectName: "StorageDatabaseExposure");
        }
    }

    private static void ValidateRead(StorageStrategyContext storageStrategyContext)
    {
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext);
        ArgumentNullException.ThrowIfNull(argument: storageStrategyContext.State);
        ObjectDisposedException.ThrowIf(condition: storageStrategyContext.State.Disposed, instance: storageStrategyContext.State);

        if (storageStrategyContext.Operation is < 10 or > 12)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(storageStrategyContext.Operation));
        }
    }
}