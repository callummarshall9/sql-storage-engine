// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Foundations.BackupFiles;
using sql_storage_engine.Services.Foundations.CompatibilityFixtures;
using sql_storage_engine.Brokers.SecuritySerialization;

namespace sql_storage_engine.Services.Orchestrations.CompatibilityFiles;
internal sealed partial class CompatibilityFileOrchestrationService : ICompatibilityFileOrchestrationService
{
    private readonly BackupFileContextService files = BackupFileContextService.CreateBackupFileContextService(backupConfiguration: new BackupConfiguration());
    private readonly CompatibilityFixtureService fixtures = new CompatibilityFixtureService(serialization: new SecuritySerializationBroker());
    public ValueTask<CompatibilityContext> ReadCompatibilityContextAsync(CompatibilityContext compatibilityContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCompatibilityContext(compatibilityContext: compatibilityContext, cancellationToken: cancellationToken);

        var length = files.ReadLengthBackupFileContext(backupFileContext: new BackupFileContext { Path = compatibilityContext.FixturePath })
            .Length;

        if (length is null or <= 0 or > 1024 * 1024)
        {
            throw new StorageFormatException(message: "Compatibility fixture size is invalid.");
        }

        var read = await files.ReadBytesBackupFileContextAsync(backupFileContext: new BackupFileContext { Path = compatibilityContext.FixturePath }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return fixtures.DecodeCompatibilityContext(compatibilityContext: compatibilityContext with { Bytes = read.Bytes });
    });

    public ValueTask WriteCompatibilityContextAsync(CompatibilityContext compatibilityContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCompatibilityContext(compatibilityContext: compatibilityContext, cancellationToken: cancellationToken);

        await files.WriteBytesBackupFileContextAsync(backupFileContext: new BackupFileContext { Path = compatibilityContext.DatabasePath, Bytes = compatibilityContext.Bytes }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}