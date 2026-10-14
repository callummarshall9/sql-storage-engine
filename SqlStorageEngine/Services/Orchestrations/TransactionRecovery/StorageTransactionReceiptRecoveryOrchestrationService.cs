// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Storage;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models;
using sql_storage_engine.Services.Processings.StatementJournals;
using sql_storage_engine.Services.Processings.TransactionReceipts;
using sql_storage_engine.Services.Processings.SavepointFiles;

namespace sql_storage_engine.Services.Orchestrations.TransactionRecovery;
internal sealed partial class StorageTransactionReceiptRecoveryOrchestrationService(TransactionDirectoryStatementJournalProcessingService journals, TransactionDirectoryReceiptProcessingService receipts, TransactionDirectorySavepointProcessingService savepointFiles) : IStorageTransactionReceiptRecoveryOrchestrationService
{
    public ValueTask<EngineResourceContext> CreateJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return engineResourceContext with
        {
            Journal = await journals.CreateStatementJournalStateAsync(databasePath: engineResourceContext.Path!, cancellationToken: cancellationToken, explicitPath: engineResourceContext.ExplicitPath)
            .ConfigureAwait(continueOnCapturedContext: false)
        };
    });

    public ValueTask<EngineResourceContext> CommitJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        await journals.CommitStatementJournalStateAsync(statementJournalState: engineResourceContext.Journal!, cancellationToken: cancellationToken, retain: engineResourceContext.Retain)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext;
    });

    public ValueTask<EngineResourceContext> RestoreJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        await journals.RestoreStatementJournalStateAsync(statementJournalState: engineResourceContext.Journal!, pageSize: engineResourceContext.PageSize, writePage: engineResourceContext.WritePage!, restoreLength: engineResourceContext.RestoreLength!, flush: engineResourceContext.Flush!, reloadHeader: engineResourceContext.ReloadHeader!, cancellationToken: cancellationToken, retain: engineResourceContext.Retain)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext;
    });

    public ValueTask<EngineResourceContext> CloseJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        await journals.ReleaseStatementJournalStateAsync(statementJournalState: engineResourceContext.Journal!)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext;
    });

    public EngineResourceContext RemoveJournalEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext);
        journals.RemoveStatementJournalState(statementJournalState: engineResourceContext.Journal!);
        return engineResourceContext;
    });

    public ValueTask<EngineResourceContext> ReadJournalSnapshotEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        var read = await journals.ReadStatementJournalStateSnapshotAsync(path: engineResourceContext.Path!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext with
        {
            SnapshotState = read.State,
            Bytes = read.Bytes
        };
    });

    public ValueTask<EngineResourceContext> ReadIdentityEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        var identity = await journals.ReadStatementJournalStateIdentityAsync(path: engineResourceContext.Path!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext with
        {
            Receipt = new StorageTransactionReceipt
            {
                Identity = new StorageTransactionIdentity
                {
                    Value = identity.Value,
                    DatabaseId = new sql_storage_engine.Models.Identifiers.DatabaseId
                    {
                        Value = identity.DatabaseId
                    }
                }
            }
        };
    });

    public EngineResourceContext ReadFileEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext);

        return engineResourceContext with
        {
            Exists = journals.CheckStatementJournalStateFile(path: engineResourceContext.Path!),
            Length = journals.CheckStatementJournalStateFile(path: engineResourceContext.Path!) ? journals.ReadStatementJournalStateFileLength(path: engineResourceContext.Path!) : 0
        };
    });

    public EngineResourceContext RemoveFileEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext);
        journals.DeleteStatementJournalStateFile(path: engineResourceContext.Path!);
        return engineResourceContext;
    });

    public ValueTask<EngineResourceContext> RecoverJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        await journals.RecoverStatementJournalStateAsync(databasePath: engineResourceContext.Path!, cancellationToken: cancellationToken, explicitPath: engineResourceContext.ExplicitPath, retain: engineResourceContext.Retain)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext;
    });

    public EngineResourceContext RemoveSavepointsEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext);
        savepointFiles.DeleteAllSavepointFiles(databasePath: engineResourceContext.Path!);
        return engineResourceContext;
    });

    public ValueTask<EngineResourceContext> WriteMetadataEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateEngineResourceMetadataContext(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        await journals.WriteStatementJournalStateIdentityAsync(path: engineResourceContext.Path!, transactionId: engineResourceContext.Metadata!.Identity.Value, databaseId: engineResourceContext.Metadata.Identity.DatabaseId.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext;
    });

    public ValueTask<EngineResourceContext> ResolveReceiptEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return engineResourceContext with
        {
            Receipt = await receipts.ResolveStorageTransactionReceiptAsync(path: engineResourceContext.Path!, storageTransactionReceipt: engineResourceContext.Receipt!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
        };
    });

    public ValueTask<EngineResourceContext> WriteReceiptEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        await receipts.WriteStorageTransactionReceiptAsync(path: engineResourceContext.Path!, storageTransactionReceipt: engineResourceContext.Receipt!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext;
    });

    public EngineResourceContext ReleaseReceiptEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext);
        receipts.ReleaseStorageTransactionReceipt(path: engineResourceContext.Path!, storageTransactionReceipt: engineResourceContext.Receipt!);
        return engineResourceContext;
    });

    internal static StorageTransactionReceiptRecoveryOrchestrationService CreateStorageTransactionReceiptRecoveryOrchestrationService() =>
        new(journals: sql_storage_engine.Services.Processings.StatementJournals.TransactionDirectoryStatementJournalProcessingService.CreateTransactionDirectoryStatementJournalProcessingService(), receipts: sql_storage_engine.Services.Processings.TransactionReceipts.TransactionDirectoryReceiptProcessingService.CreateTransactionDirectoryReceiptProcessingService(), savepointFiles: sql_storage_engine.Services.Processings.SavepointFiles.TransactionDirectorySavepointProcessingService.CreateTransactionDirectorySavepointProcessingService());

    public EngineResourceContext GenerateSavepointPathEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateEngineResourceContext(engineResourceContext: engineResourceContext);

        return engineResourceContext with
        {
            TextResult = savepointFiles.GenerateSavepointPath(databasePath: engineResourceContext.Path!)
        };
    });
}