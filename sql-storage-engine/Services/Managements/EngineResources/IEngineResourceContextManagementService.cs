// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Managements.EngineResources;
internal partial interface IEngineResourceContextManagementService
{
    EngineResourceContext AcquireEngineResourceContextFile(EngineResourceContext engineResourceContext);

    EngineResourceContext CloseEngineResourceContextFile(EngineResourceContext engineResourceContext);

    EngineResourceContext ReadEngineResourceContextAccess(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> EnterEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext EnterImmediateEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext ActivateEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext ReleaseEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> DrainEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext RegisterScopeEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> EnterSecurityEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext ReleaseSecurityEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> FlushBufferEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> DiscardBufferEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> CloseBufferEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> CreateJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> CommitJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> CloseJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext RemoveJournalEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> ReadJournalSnapshotEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> ReadIdentityEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext ReadFileEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext RemoveFileEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> RecoverJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext RemoveSavepointsEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> WriteMetadataEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> ResolveReceiptEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> WriteReceiptEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext ReleaseReceiptEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext ReadSnapshotIdentityEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> RecoverEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> FlushDatabaseEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> ReloadHeaderEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext ReadEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext RestoreLengthEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext SetMaximumLengthEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> PublishCatalogRootEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> CreateEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> OpenEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> CloseEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> RestoreJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext TrackEngineResourceContextStream<T>(EngineResourceContext engineResourceContext);

    EngineResourceContext CreateTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext StartTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext CancelTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext CloseTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext EnterTransactionOperationEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> WaitTransactionOperationEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext ReleaseTransactionOperationEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext LinkTransactionTokenEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext ReadTransactionTokenEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> WaitTransactionTaskEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext GenerateSavepointPathEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext GenerateTransactionIdentityEngineResourceContext(EngineResourceContext engineResourceContext);
}