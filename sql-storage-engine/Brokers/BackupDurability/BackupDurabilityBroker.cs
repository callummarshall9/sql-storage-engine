// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Dependencies.BackupDurability;

namespace sql_storage_engine.Brokers.BackupDurability;
internal sealed class BackupDurabilityBroker : IBackupDurabilityBroker
{
    private readonly BackupDurabilityDependency dependency;
    public BackupDurabilityBroker(BackupConfiguration backupConfiguration)
    {
        dependency = new BackupDurabilityDependency(current: backupConfiguration.CurrentDurableLsnSource);
    }

    public ulong ReadCurrent() =>
        dependency.ReadCurrent();

    public ValueTask InvokeAsync(Func<CancellationToken, ValueTask>? callback, CancellationToken cancellationToken) =>
        dependency.InvokeAsync(callback: callback, cancellationToken: cancellationToken);
}