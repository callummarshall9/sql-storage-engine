// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Coordinations.EngineDurabilities;
internal interface IEngineDurabilityResourceCoordinationService
{
    ValueTask<EngineResourceContext> CreateJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> CommitJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> RestoreJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

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

    EngineResourceContext ReadTransactionJournalPathEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext ReadTransactionActivePathEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext OpenOverflowEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext GenerateSavepointPathEngineResourceContext(EngineResourceContext engineResourceContext);
}