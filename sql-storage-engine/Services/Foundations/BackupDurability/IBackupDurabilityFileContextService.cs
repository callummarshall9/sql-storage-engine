// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Foundations.BackupDurability;
internal interface IBackupDurabilityFileContextService
{
    BackupFileContext ReadBackupFileContextDurability(BackupFileContext backupFileContext);

    ValueTask<BackupFileContext> InvokeBackupFileContextStartAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken);
}