// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Brokers.BackupFiles;

namespace sql_storage_engine.Services.Foundations.BackupFiles;
internal sealed partial class BackupFileContextService : IBackupFileContextService
{
    private readonly BackupFileBroker broker;
    public BackupFileContextService(BackupFileBroker broker)
    {
        this.broker = broker;
    }

    internal static BackupFileContextService CreateBackupFileContextService(BackupConfiguration backupConfiguration) =>
        new BackupFileContextService(broker: new BackupFileBroker(backupConfiguration: backupConfiguration));

    public BackupFileContext ReadFullPathBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            TextResult = broker.GetFullPath(path: backupFileContext.Path!)
        };
    });

    public BackupFileContext ReadCombinedPathBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            TextResult = broker.CombinePath(directory: backupFileContext.Directory!, name: backupFileContext.Name!)
        };
    });

    public BackupFileContext ReadFileNameBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            TextResult = broker.GetFileName(path: backupFileContext.Path!)
        };
    });

    public BackupFileContext ReadExistenceBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            Exists = broker.Exists(path: backupFileContext.Path!)
        };
    });

    public BackupFileContext ReadLengthBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            Length = broker.ReadLength(path: backupFileContext.Path!)
        };
    });

    public BackupFileContext ReadUtcNowBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            CreatedAt = broker.ReadUtcNow()
        };
    });

    public BackupFileContext SerializeManifestBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            Bytes = broker.Serialize(value: backupFileContext.Manifest!, type: typeof(BackupManifest))
        };
    });

    public BackupFileContext DeserializeManifestBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            Manifest = (BackupManifest? )broker.Deserialize(value: backupFileContext.Bytes!, type: typeof(BackupManifest))
        };
    });

    public BackupFileContext MakeDirectoryBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        broker.CreateDirectory(path: backupFileContext.Path!);
        return backupFileContext;
    });

    public BackupFileContext RemoveDirectoryBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        broker.DeleteDirectory(path: backupFileContext.Path!);
        return backupFileContext;
    });

    public ValueTask<BackupFileContext> ReadBytesBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return backupFileContext with
        {
            Bytes = await broker.ReadBytesAsync(path: backupFileContext.Path!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
        };
    });

    public ValueTask<BackupFileContext> ReadPageBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return backupFileContext with
        {
            Bytes = await broker.ReadPageAsync(path: backupFileContext.Path!, pageId: backupFileContext.PageId, pageSize: backupFileContext.PageSize, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
        };
    });

    public ValueTask<BackupFileContext> WriteBytesBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        await broker.WriteBytesAsync(path: backupFileContext.Path!, bytes: backupFileContext.Bytes!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return backupFileContext;
    });

    public ValueTask<BackupFileContext> CopyBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        await broker.CopyAsync(source: backupFileContext.SourcePath!, destination: backupFileContext.DestinationPath!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return backupFileContext;
    });

    public ValueTask<BackupFileContext> DescribeBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        var descriptor = await broker.DescribeAsync(path: backupFileContext.Path!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return backupFileContext with
        {
            Length = descriptor.Size,
            TextResult = descriptor.Sha256
        };
    });

    public BackupFileContext MatchChecksumBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            ChecksumMatches = broker.AreHashesEqual(left: backupFileContext.TextResult!, right: backupFileContext.ExpectedSha256!)
        };
    });

    public BackupFileContext ReadEngineVersionBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            TextResult = broker.ReadAssemblyVersion(type: typeof(BackupManifest))
        };
    });

    public BackupFileContext SerializeValueBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            Bytes = broker.Serialize(value: backupFileContext.ValueSource!, type: backupFileContext.ValueType!)
        };
    });

    public BackupFileContext DeserializeValueBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);

        return backupFileContext with
        {
            ValueResult = broker.Deserialize(value: backupFileContext.Bytes!, type: backupFileContext.ValueType!)
        };
    });
}