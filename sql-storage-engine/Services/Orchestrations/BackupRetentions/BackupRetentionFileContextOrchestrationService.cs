// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Services.Foundations.BackupDurability;
using sql_storage_engine.Services.Foundations.WalRetentions;

namespace sql_storage_engine.Services.Orchestrations.BackupRetentions;
internal sealed partial class BackupRetentionFileContextOrchestrationService : IBackupRetentionFileContextOrchestrationService
{
    private readonly WalRetentionLogSequenceNumberService retention;
    private readonly BackupDurabilityFileContextService durability;
    private readonly BackupConfiguration configuration;
    public BackupRetentionFileContextOrchestrationService(WalRetentionLogSequenceNumberService retention, BackupDurabilityFileContextService durability, BackupConfiguration backupConfiguration)
    {
        this.retention = retention;
        this.durability = durability;
        configuration = backupConfiguration;
    }

    internal static BackupRetentionFileContextOrchestrationService CreateBackupRetentionFileContextOrchestrationService(BackupConfiguration backupConfiguration) =>
        new BackupRetentionFileContextOrchestrationService(retention: backupConfiguration.RetentionBinding?.Source as WalRetentionLogSequenceNumberService ?? new WalRetentionLogSequenceNumberService(), durability: BackupDurabilityFileContextService.CreateBackupDurabilityFileContextService(backupConfiguration: backupConfiguration), backupConfiguration: backupConfiguration);

    public BackupFileContext RegisterBackupFileContextRetention(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        var current = durability.ReadBackupFileContextDurability(backupFileContext: backupFileContext)
            .CurrentLsn;

        return backupFileContext with
        {
            StartLsn = current,
            RetentionRegistrationId = retention.RegisterWalRetention(logSequenceNumber: current.Value, registrationId: configuration.SourceRegistrationId)
        };
    });

    public BackupFileContext ReleaseBackupFileContextRetention(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        retention.ReleaseWalRetention(registrationId: backupFileContext.RetentionRegistrationId);
        return backupFileContext;
    });

    public ValueTask<BackupFileContext> StartBackupFileContextCopyAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        await durability.InvokeBackupFileContextStartAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var current = durability.ReadBackupFileContextDurability(backupFileContext: backupFileContext)
            .CurrentLsn;

        if (current.Value < backupFileContext.StartLsn!.Value.Value)
        {
            throw new InvalidOperationException(message: "The durable WAL position moved backwards during backup.");
        }

        return backupFileContext with
        {
            EndLsn = current,
            RequireCleanShutdown = false
        };
    });
}