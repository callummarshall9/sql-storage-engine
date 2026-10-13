// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Brokers.BackupDurability;

namespace sql_storage_engine.Services.Foundations.BackupDurability;
internal sealed partial class BackupDurabilityFileContextService : IBackupDurabilityFileContextService
{
    private readonly BackupDurabilityBroker broker;
    public BackupDurabilityFileContextService(BackupDurabilityBroker broker)
    {
        this.broker = broker;
    }

    internal static BackupDurabilityFileContextService CreateBackupDurabilityFileContextService(BackupConfiguration backupConfiguration) =>
        new BackupDurabilityFileContextService(broker: new BackupDurabilityBroker(backupConfiguration: backupConfiguration));

    public BackupFileContext ReadBackupFileContextDurability(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            CurrentLsn = new LogSequenceNumber
            {
                Value = broker.ReadCurrent()
            }
        };
    });

    public ValueTask<BackupFileContext> InvokeBackupFileContextStartAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        await broker.InvokeAsync(callback: backupFileContext.AfterStart, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return backupFileContext;
    });
}