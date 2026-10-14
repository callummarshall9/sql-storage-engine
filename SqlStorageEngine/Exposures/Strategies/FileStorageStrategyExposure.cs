// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Managements.StorageStrategies;

namespace sql_storage_engine.Exposures.Strategies;
public sealed class FileStorageStrategyExposure : IStorageStrategyExposure, IStorageDatabaseExposure, ICompositionExposure
{
    private readonly StorageStrategyContextManagementService service;
    private readonly StorageSessionState? state;
    public FileStorageStrategyExposure(StorageStrategyOptions options)
    {
        service = StorageStrategyContextManagementService.CreateStorageStrategyContextManagementService(storageStrategyContext: new StorageStrategyContext { Options = options });
    }

    internal FileStorageStrategyExposure(StorageStrategyContext storageStrategyContext)
    {
        service = (StorageStrategyContextManagementService)storageStrategyContext.Service!;
        state = storageStrategyContext.State!;
    }

    public async ValueTask<IStorageDatabaseExposure> CreateStorageDatabaseExposureAsync(string name, int pageSize = PageConstants.DefaultSize, CancellationToken cancellationToken = default) =>
        new FileStorageStrategyExposure(storageStrategyContext: await service.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { Operation = 0, Name = name, PageSize = pageSize }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

    public async ValueTask<IStorageDatabaseExposure> OpenAsync(string name, CancellationToken cancellationToken = default) =>
        new FileStorageStrategyExposure(storageStrategyContext: await service.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { Operation = 1, Name = name }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

    public int GetPageSize() =>
        service.ReadStorageStrategyContext(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 10 }).PageSize;

    public DatabaseHeader ReadHeader() =>
        service.ReadStorageStrategyContext(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 11 }).Header!;

    public StorageCommitStatus ReadCommitStatus() =>
        service.ReadStorageStrategyContext(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 12 }).Status!;

    public ValueTask ReadAsync(PageId pageId, Memory<byte> destination, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 2, PageId = pageId.Value, Destination = destination }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask WriteAsync(PageId pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 3, PageId = pageId.Value, Source = source }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 4 }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask CommitAsync(CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 8 }, cancellationToken: cancellationToken)
            .AsTask());

    public async ValueTask<PageId> AllocateAsync(PageType pageType, CancellationToken cancellationToken = default) =>
        new PageId
        {
            Value = (await service.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 5, PageType = pageType }, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false)).PageId
        };

    public ValueTask FreeAsync(PageId pageId, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 6, PageId = pageId.Value }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask PublishCatalogRootAsync(PageId pageId, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 7, PageId = pageId.Value }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask DisposeAsync() =>
        new ValueTask(task: service.ProcessStorageStrategyContextAsync(storageStrategyContext: new StorageStrategyContext { State = state, Operation = 9 }, cancellationToken: CancellationToken.None)
            .AsTask());
}