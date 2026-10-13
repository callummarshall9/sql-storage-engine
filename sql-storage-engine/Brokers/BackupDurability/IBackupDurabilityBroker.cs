// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.BackupDurability;
internal interface IBackupDurabilityBroker
{
    ulong ReadCurrent();

    ValueTask InvokeAsync(Func<CancellationToken, ValueTask>? callback, CancellationToken cancellationToken);
}