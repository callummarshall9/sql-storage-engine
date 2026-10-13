// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using sql_storage_engine.Buffers;
using sql_storage_engine.Exposures.Catalog;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Heap;
using sql_storage_engine.Indexes;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Exposures.Buffers;
using sql_storage_engine.Services.Aggregations.StorageSessions;

namespace sql_storage_engine.UnitTests;
internal static class CatalogSessionTestResources
{
    internal static StorageSessionConfiguration CreateStorageSessionConfiguration(IPageStoreExposure pageStore, IPageAllocatorExposure allocator, IBufferPoolExposure bufferPool, StorageGenerationConfiguration? generation, int inlineThreshold = 0) =>
        new StorageSessionConfiguration
    {
        Generation = generation,
        Rows = new RowEncodingConfiguration
        {
            UseOverflow = true,
            InlineThreshold = inlineThreshold
        },
        Overflow = new OverflowPageResourceConfiguration
        {
            BufferSource = bufferPool,
            AllocatorSource = allocator,
            PageSize = () => bufferPool.ReadPageSize(),
            Pin = (id, token) => bufferPool.GetPageLeaseAsync(pageId: new PageId { Value = id }, cancellationToken: token),
            Allocate = async token => (await allocator.AllocateAsync(pageType: PageType.Overflow, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).Value,
            Discard = (id, token) => bufferPool.DiscardPageAsync(pageId: new PageId { Value = id }, cancellationToken: token),
            Free = (id, token) => allocator.FreeAsync(pageId: new PageId { Value = id }, cancellationToken: token)
        },
        CatalogStorage = new CatalogStorageConfiguration
        {
            Pages = new CatalogPageResourceConfiguration
            {
                PageSize = pageStore.GetPageSize,
                Allocate = async (type, token) => (await allocator.AllocateAsync(pageType: (PageType)type, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).Value,
                Free = (id, token) => allocator.FreeAsync(pageId: new PageId { Value = id }, cancellationToken: token),
                Read = (id, bytes, token) => pageStore.ReadAsync(pageId: new PageId { Value = id }, destination: bytes, cancellationToken: token),
                Write = (id, bytes, token) => pageStore.WriteAsync(pageId: new PageId { Value = id }, source: bytes, cancellationToken: token)
            },
            Buffer = new CatalogBufferConfiguration
            {
                Flush = bufferPool.FlushAllAsync,
                Discard = (id, token) => bufferPool.DiscardPageAsync(pageId: new PageId { Value = id }, cancellationToken: token)
            },
            Heap = new HeapPageResourceConfiguration
            {
                BufferSource = bufferPool,
                AllocatorSource = allocator,
                PageSize = () => bufferPool.ReadPageSize(),
                Pin = (id, token) => bufferPool.GetPageLeaseAsync(pageId: new PageId { Value = id }, cancellationToken: token),
                Allocate = async token => (await allocator.AllocateAsync(pageType: PageType.Heap, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).Value,
                Discard = (id, token) => bufferPool.DiscardPageAsync(pageId: new PageId { Value = id }, cancellationToken: token),
                Free = (id, token) => allocator.FreeAsync(pageId: new PageId { Value = id }, cancellationToken: token)
            },
            Tree = new IndexTreeResourceConfiguration
            {
                BufferSource = bufferPool,
                AllocatorSource = allocator,
                PageSize = () => bufferPool.ReadPageSize(),
                Pin = (id, token) => bufferPool.GetPageLeaseAsync(pageId: new PageId { Value = id }, cancellationToken: token),
                Allocate = async (type, token) => (await allocator.AllocateAsync(pageType: (PageType)type, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).Value
            }
        }
    };
}