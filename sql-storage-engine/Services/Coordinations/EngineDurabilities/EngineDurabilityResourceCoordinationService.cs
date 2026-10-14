// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Exceptions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Coordinations.EngineDurabilities;
internal sealed partial class EngineDurabilityResourceCoordinationService : IEngineDurabilityResourceCoordinationService
{
    private readonly sql_storage_engine.Services.Orchestrations.TransactionRecovery.StorageTransactionReceiptRecoveryOrchestrationService transactions;
    private readonly sql_storage_engine.Services.Orchestrations.DatabaseHeaders.DatabaseHeaderOrchestrationService headers;
    private sql_storage_engine.Services.Orchestrations.OverflowChains.OverflowChainContextOrchestrationService? overflow;
    private EngineDurabilityResourceCoordinationService(sql_storage_engine.Services.Orchestrations.TransactionRecovery.StorageTransactionReceiptRecoveryOrchestrationService transactions, sql_storage_engine.Services.Orchestrations.DatabaseHeaders.DatabaseHeaderOrchestrationService headers, sql_storage_engine.Services.Orchestrations.OverflowChains.OverflowChainContextOrchestrationService overflow)
    {
        this.transactions = transactions;
        this.headers = headers;
        this.overflow = overflow;
    }

    internal static EngineDurabilityResourceCoordinationService CreateEngineDurabilityResourceCoordinationService() =>
        new(transactions: sql_storage_engine.Services.Orchestrations.TransactionRecovery.StorageTransactionReceiptRecoveryOrchestrationService.CreateStorageTransactionReceiptRecoveryOrchestrationService(), headers: new sql_storage_engine.Services.Orchestrations.DatabaseHeaders.DatabaseHeaderOrchestrationService(), overflow: null!);

    public ValueTask<EngineResourceContext> CreateJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await transactions.CreateJournalEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> CommitJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await transactions.CommitJournalEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> RestoreJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await transactions.RestoreJournalEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> CloseJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await transactions.CloseJournalEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext RemoveJournalEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return transactions.RemoveJournalEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> ReadJournalSnapshotEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await transactions.ReadJournalSnapshotEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> ReadIdentityEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await transactions.ReadIdentityEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext ReadFileEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return transactions.ReadFileEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext RemoveFileEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return transactions.RemoveFileEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> RecoverJournalEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await transactions.RecoverJournalEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext RemoveSavepointsEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return transactions.RemoveSavepointsEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> WriteMetadataEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await transactions.WriteMetadataEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> ResolveReceiptEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await transactions.ResolveReceiptEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<EngineResourceContext> WriteReceiptEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await transactions.WriteReceiptEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext ReleaseReceiptEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return transactions.ReleaseReceiptEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ReadSnapshotIdentityEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        var bytes = engineResourceContext.Bytes!;

        if (bytes.Length < DatabaseHeaderFormat.PayloadOffset + 32)
        {
            throw new StorageFormatException(message: "Transaction snapshot header is invalid.");
        }

        var size = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(source: bytes.AsSpan(start: DatabaseHeaderFormat.PayloadOffset + 28));

        if (!Enum.IsDefined(value: (PageSize)size) || bytes.Length < size)
        {
            throw new StorageFormatException(message: "Transaction snapshot header is invalid.");
        }

        return engineResourceContext with
        {
            DatabaseIdentity = headers.ReadDatabaseHeader(page: bytes.AsSpan(start: 0, length: size))
            .DatabaseId.Value
        };
    });

    public ValueTask<EngineResourceContext> RecoverEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);
        var path = engineResourceContext.Path!;
        var active = path + ".transaction-active";
        var journal = path + ".transaction-undo";

        if (!transactions.ReadFileEngineResourceContext(engineResourceContext: new EngineResourceContext { Path = active })
            .Exists)
        {
            if (transactions.ReadFileEngineResourceContext(engineResourceContext: new EngineResourceContext { Path = journal })
            .Exists)
            {
                await transactions.RecoverJournalEngineResourceContextAsync(engineResourceContext: new EngineResourceContext { Path = path, ExplicitPath = journal }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            transactions.RemoveSavepointsEngineResourceContext(engineResourceContext: engineResourceContext);
            return engineResourceContext;
        }

        var identity = (await transactions.ReadIdentityEngineResourceContextAsync(engineResourceContext: new EngineResourceContext { Path = active }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Receipt!.Identity;

        if (identity.Value == Guid.Empty || identity.DatabaseId.Value == Guid.Empty)
        {
            throw new TransactionRecoveryEvidenceException(message: "Transaction identity is invalid.");
        }

        var receipt = (await transactions.ResolveReceiptEngineResourceContextAsync(engineResourceContext: new EngineResourceContext { Path = path, Receipt = new StorageTransactionReceipt { Identity = identity } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Receipt!;

        if (transactions.ReadFileEngineResourceContext(engineResourceContext: new EngineResourceContext { Path = journal })
            .Exists)
        {
            var snapshot = await transactions.ReadJournalSnapshotEngineResourceContextAsync(engineResourceContext: new EngineResourceContext { Path = journal }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            var bytes = snapshot.Bytes!;

            if (bytes.Length < DatabaseHeaderFormat.PayloadOffset + 32)
            {
                throw new StorageFormatException(message: "Transaction snapshot header is invalid.");
            }

            var size = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(source: bytes.AsSpan(start: DatabaseHeaderFormat.PayloadOffset + 28));

            if (!Enum.IsDefined(value: (PageSize)size) || bytes.Length < size)
            {
                throw new StorageFormatException(message: "Transaction snapshot header is invalid.");
            }

            if (headers.ReadDatabaseHeader(page: bytes.AsSpan(start: 0, length: size))
            .DatabaseId != identity.DatabaseId)
            {
                throw new TransactionRecoveryEvidenceException(message: "Transaction journal belongs to another database.");
            }

            var committed = snapshot.SnapshotState == 2;

            if (receipt.State != StorageTransactionState.Indeterminate && receipt.State != (committed ? StorageTransactionState.Committed : StorageTransactionState.Aborted))
            {
                throw new TransactionRecoveryEvidenceException(message: "Transaction decision contradicts its receipt.");
            }

            await transactions.RecoverJournalEngineResourceContextAsync(engineResourceContext: new EngineResourceContext { Path = path, ExplicitPath = journal, Retain = true }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            receipt = new StorageTransactionReceipt
            {
                Identity = identity,
                State = committed ? StorageTransactionState.Committed : StorageTransactionState.Aborted
            };

            await transactions.WriteReceiptEngineResourceContextAsync(engineResourceContext: new EngineResourceContext { Path = path, Receipt = receipt }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            transactions.RemoveFileEngineResourceContext(engineResourceContext: new EngineResourceContext { Path = path + ".statement-undo" });
            transactions.RemoveFileEngineResourceContext(engineResourceContext: new EngineResourceContext { Path = journal });
        }
        else if (receipt.State == StorageTransactionState.Indeterminate)
        {
            throw new TransactionRecoveryEvidenceException(message: "An unresolved transaction has no recovery evidence.");
        }

        transactions.RemoveSavepointsEngineResourceContext(engineResourceContext: engineResourceContext);
        transactions.RemoveFileEngineResourceContext(engineResourceContext: new EngineResourceContext { Path = active });
        return engineResourceContext;
    });

    public EngineResourceContext ReadTransactionJournalPathEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: engineResourceContext.Path);

        return engineResourceContext with
        {
            TextResult = engineResourceContext.Path + ".transaction-undo"
        };
    });

    public EngineResourceContext ReadTransactionActivePathEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: engineResourceContext.Path);

        return engineResourceContext with
        {
            TextResult = engineResourceContext.Path + ".transaction-active"
        };
    });

    public EngineResourceContext OpenOverflowEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        overflow = sql_storage_engine.Services.Orchestrations.OverflowChains.OverflowChainContextOrchestrationService.CreateOverflowChainContextOrchestrationService(overflowPageResourceConfiguration: engineResourceContext.OverflowConfiguration!);

        return engineResourceContext;
    });

    public EngineResourceContext GenerateSavepointPathEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return transactions.GenerateSavepointPathEngineResourceContext(engineResourceContext: engineResourceContext);
    });
}