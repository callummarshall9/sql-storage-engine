// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Services.Orchestrations.BackupFiles;
using sql_storage_engine.Services.Orchestrations.BackupArchives;
using sql_storage_engine.Services.Orchestrations.BackupRetentions;

namespace sql_storage_engine.Services.Coordinations.BackupFiles;
internal sealed partial class BackupFileContextCoordinationService : IBackupFileContextCoordinationService
{
    private readonly BackupFileContextOrchestrationService files;
    private readonly BackupArchiveFileContextOrchestrationService archives;
    private readonly BackupRetentionFileContextOrchestrationService retention;
    public BackupFileContextCoordinationService(BackupFileContextOrchestrationService files, BackupArchiveFileContextOrchestrationService archives, BackupRetentionFileContextOrchestrationService retention)
    {
        this.files = files;
        this.archives = archives;
        this.retention = retention;
    }

    internal static BackupFileContextCoordinationService CreateBackupFileContextCoordinationService(BackupConfiguration backupConfiguration) =>
        new BackupFileContextCoordinationService(files: BackupFileContextOrchestrationService.CreateBackupFileContextOrchestrationService(backupConfiguration: backupConfiguration), archives: BackupArchiveFileContextOrchestrationService.CreateBackupArchiveFileContextOrchestrationService(), retention: BackupRetentionFileContextOrchestrationService.CreateBackupRetentionFileContextOrchestrationService(backupConfiguration: backupConfiguration));

    public BackupFileContext ReadFullPathBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.ReadFullPathBackupFileContext(backupFileContext: backupFileContext);
    });

    public BackupFileContext ReadCombinedPathBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.ReadCombinedPathBackupFileContext(backupFileContext: backupFileContext);
    });

    public BackupFileContext ReadFileNameBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.ReadFileNameBackupFileContext(backupFileContext: backupFileContext);
    });

    public BackupFileContext ReadExistenceBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.ReadExistenceBackupFileContext(backupFileContext: backupFileContext);
    });

    public BackupFileContext ReadLengthBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.ReadLengthBackupFileContext(backupFileContext: backupFileContext);
    });

    public BackupFileContext ReadUtcNowBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.ReadUtcNowBackupFileContext(backupFileContext: backupFileContext);
    });

    public BackupFileContext ReadEngineVersionBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.ReadEngineVersionBackupFileContext(backupFileContext: backupFileContext);
    });

    public BackupFileContext MatchChecksumBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.MatchChecksumBackupFileContext(backupFileContext: backupFileContext);
    });

    public BackupFileContext MakeDirectoryBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.MakeDirectoryBackupFileContext(backupFileContext: backupFileContext);
    });

    public BackupFileContext RemoveDirectoryBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.RemoveDirectoryBackupFileContext(backupFileContext: backupFileContext);
    });

    public BackupFileContext SerializeTimelineBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.SerializeTimelineBackupFileContext(backupFileContext: backupFileContext);
    });

    public BackupFileContext DeserializeTimelineBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.DeserializeTimelineBackupFileContext(backupFileContext: backupFileContext);
    });

    public ValueTask<BackupFileContext> ReadBytesBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return await files.ReadBytesBackupFileContextAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> ReadPageBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return await files.ReadPageBackupFileContextAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> WriteBytesBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return await files.WriteBytesBackupFileContextAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> CopyBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return await files.CopyBackupFileContextAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> DescribeBackupFileContextAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return await files.DescribeBackupFileContextAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> ReadBackupFileContextManifestAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return await files.ReadBackupFileContextManifestAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> WriteBackupFileContextManifestAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return await files.WriteBackupFileContextManifestAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> ReadBackupFileContextVerificationAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return await files.ReadBackupFileContextVerificationAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public BackupFileContext ReadBackupFileContextRecords(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return archives.ReadBackupFileContextRecords(backupFileContext: backupFileContext);
    });

    public BackupFileContext WriteBackupFileContextArchive(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return archives.WriteBackupFileContextArchive(backupFileContext: backupFileContext);
    });

    public BackupFileContext RegisterBackupFileContextRetention(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return retention.RegisterBackupFileContextRetention(backupFileContext: backupFileContext);
    });

    public BackupFileContext ReleaseBackupFileContextRetention(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return retention.ReleaseBackupFileContextRetention(backupFileContext: backupFileContext);
    });

    public ValueTask<BackupFileContext> StartBackupFileContextCopyAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return await retention.StartBackupFileContextCopyAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> ReadBackupFileContextArchiveAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        var read = await files.ReadBytesBackupFileContextAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return archives.ReadBackupFileContextArchive(backupFileContext: read);
    });

    public ValueTask<BackupFileContext> ReadBackupFileContextTimelineAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        var read = await files.ReadBytesBackupFileContextAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return files.DeserializeTimelineBackupFileContext(backupFileContext: read);
    });

    public ValueTask<BackupFileContext> WriteBackupFileContextTimelineAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);
        var encoded = files.SerializeTimelineBackupFileContext(backupFileContext: backupFileContext);

        return await files.WriteBytesBackupFileContextAsync(backupFileContext: encoded, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}