// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Services.Foundations.BackupFiles;
using System.Linq;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.BackupManifests;
internal sealed partial class BackupManifestBackupFileContextProcessingService : IBackupManifestBackupFileContextProcessingService
{
    private const int MaximumManifestBytes = 1024 * 1024;
    private readonly BackupFileContextService files;
    public BackupManifestBackupFileContextProcessingService(BackupFileContextService files)
    {
        this.files = files;
    }

    internal static BackupManifestBackupFileContextProcessingService CreateBackupManifestBackupFileContextProcessingService(BackupConfiguration backupConfiguration) =>
        new BackupManifestBackupFileContextProcessingService(files: BackupFileContextService.CreateBackupFileContextService(backupConfiguration: backupConfiguration));

    public ValueTask<BackupFileContext> ReadBackupFileContextManifestAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        var length = files.ReadLengthBackupFileContext(backupFileContext: backupFileContext)
            .Length;

        if (length is null)
        {
            throw new StorageFormatException(message: "Backup manifest is missing.");
        }

        if (length is <= 0 or > MaximumManifestBytes)
        {
            throw new StorageFormatException(message: "Backup manifest size is invalid.");
        }

        var read = await files.ReadBytesBackupFileContextAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var decoded = files.DeserializeManifestBackupFileContext(backupFileContext: read);
        var manifest = decoded.Manifest ?? throw new StorageFormatException(message: "Backup manifest is empty.");

        if (!Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)manifest.PageSize) || manifest.Files.Count is 0 or > 1024 || manifest.Files.Any(predicate: file => file.Size < 0 || file.Name != files.ReadFileNameBackupFileContext(backupFileContext: new BackupFileContext { Path = file.Name })
            .TextResult || file.Sha256.Length != 64) || manifest.Files.Count(predicate: file => file.Kind == "database") != 1)
        {
            throw new StorageFormatException(message: "Backup manifest fields are invalid.");
        }

        return decoded;
    });

    public ValueTask<BackupFileContext> WriteBackupFileContextManifestAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateManifest(backupFileContext: backupFileContext, cancellationToken: cancellationToken);
        var encoded = files.SerializeManifestBackupFileContext(backupFileContext: backupFileContext);

        return await files.WriteBytesBackupFileContextAsync(backupFileContext: encoded, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}