// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Services.Foundations.BackupFiles;

namespace sql_storage_engine.Services.Processings.BackupFiles;
internal sealed partial class BackupFileContextProcessingService : IBackupFileContextProcessingService
{
    private readonly BackupFileContextService files;
    public BackupFileContextProcessingService(BackupFileContextService files)
    {
        this.files = files;
    }

    internal static BackupFileContextProcessingService CreateBackupFileContextProcessingService(BackupConfiguration backupConfiguration) =>
        new BackupFileContextProcessingService(files: BackupFileContextService.CreateBackupFileContextService(backupConfiguration: backupConfiguration));

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

    public BackupFileContext SerializeValueBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.SerializeValueBackupFileContext(backupFileContext: backupFileContext);
    });

    public BackupFileContext DeserializeValueBackupFileContext(BackupFileContext backupFileContext) =>
        TryCatch(operation: () =>
    {
        ValidateBackupFileContext(backupFileContext: backupFileContext);
        return files.DeserializeValueBackupFileContext(backupFileContext: backupFileContext);
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
        ValidateDescriptor(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        var described = await files.DescribeBackupFileContextAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return described with
        {
            FileResult = new BackupFile
            {
                Name = backupFileContext.Name!,
                Kind = backupFileContext.Kind!,
                Size = described.Length!.Value,
                Sha256 = described.TextResult!
            }
        };
    });
}