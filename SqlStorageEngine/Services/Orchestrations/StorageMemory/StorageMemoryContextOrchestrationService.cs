// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Foundations.PageHeaders;
using sql_storage_engine.Services.Foundations.StorageMemory;

namespace sql_storage_engine.Services.Orchestrations.StorageMemory;
internal sealed partial class StorageMemoryContextOrchestrationService(StorageMemoryContextService memory, PageHeaderService headers) : IStorageMemoryContextOrchestrationService
{
    internal static StorageMemoryContextOrchestrationService CreateStorageMemoryContextOrchestrationService() =>
        new StorageMemoryContextOrchestrationService(memory: new StorageMemoryContextService(), headers: new PageHeaderService());

    public ValueTask<StorageStrategyContext> ProcessStorageStrategyContextAsync(StorageStrategyContext storageStrategyContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageStrategyContext(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken);

        switch (storageStrategyContext.Operation)
        {
            case 120:
                var page = new byte[storageStrategyContext.State!.PageSize];
                headers.WritePageHeader(destination: page, pageHeader: new PageHeader { PageId = new PageId { Value = storageStrategyContext.PageId }, PageType = storageStrategyContext.PageType, FormatVersion = new PageFormatVersion { Value = PageHeaderFormat.CurrentVersion }, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32 });
                headers.WriteChecksum(page: page, pageSize: page.Length);
                storageStrategyContext.Bytes = page;
                break;
            case 121:
                headers.ValidateChecksum(page: storageStrategyContext.Bytes!, pageSize: storageStrategyContext.State!.PageSize);
                var header = headers.ReadPageHeader(source: storageStrategyContext.Bytes!);
                headers.CheckPageHeader(pageHeader: header, expectedPageId: storageStrategyContext.PageId);
                storageStrategyContext.PageType = header.PageType;
                break;
            case 122:
                headers.WriteChecksum(page: storageStrategyContext.Bytes!, pageSize: storageStrategyContext.State!.PageSize);
                break;
            default:
                return await memory.ProcessStorageStrategyContextAsync(storageStrategyContext: storageStrategyContext, cancellationToken: cancellationToken)
                    .ConfigureAwait(continueOnCapturedContext: false);
        }

        return storageStrategyContext;
    });
}