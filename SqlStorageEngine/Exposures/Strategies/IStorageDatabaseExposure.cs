// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Strategies;

namespace sql_storage_engine.Exposures.Strategies;
/// <summary>A page database whose successful commits are durable in the selected storage backend.</summary>
public interface IStorageDatabaseExposure : IPageStoreExposure, IPageAllocatorExposure
{
    DatabaseHeader ReadHeader();
    ValueTask PublishCatalogRootAsync(PageId pageId, CancellationToken cancellationToken = default);
    /// <summary>Flush caller-owned buffers before committing a consistent physical checkpoint.</summary>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
    StorageCommitStatus ReadCommitStatus();
}