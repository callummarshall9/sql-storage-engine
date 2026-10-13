// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Processings.BackupFiles;
using sql_storage_engine.Services.Processings.BackupManifests;

namespace sql_storage_engine.Services.Orchestrations.BackupFiles;
internal sealed partial class BackupFileContextOrchestrationService : IBackupFileContextOrchestrationService
{
    private readonly BackupFileContextProcessingService files;
    private readonly BackupManifestBackupFileContextProcessingService manifests;
    public BackupFileContextOrchestrationService(BackupFileContextProcessingService files, BackupManifestBackupFileContextProcessingService manifests)
    {
        this.files = files;
        this.manifests = manifests;
    }

    internal static BackupFileContextOrchestrationService CreateBackupFileContextOrchestrationService(BackupConfiguration backupConfiguration) =>
        new BackupFileContextOrchestrationService(files: BackupFileContextProcessingService.CreateBackupFileContextProcessingService(backupConfiguration: backupConfiguration), manifests: BackupManifestBackupFileContextProcessingService.CreateBackupManifestBackupFileContextProcessingService(backupConfiguration: backupConfiguration));

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
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        return await files.DescribeBackupFileContextAsync(backupFileContext: backupFileContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> ReadBackupFileContextManifestAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        var fullDirectory = files.ReadFullPathBackupFileContext(backupFileContext: new BackupFileContext { Path = backupFileContext.Directory })
            .TextResult!;

        var path = files.ReadCombinedPathBackupFileContext(backupFileContext: new BackupFileContext { Directory = fullDirectory, Name = "manifest.json" })
            .TextResult!;

        return await manifests.ReadBackupFileContextManifestAsync(backupFileContext: backupFileContext with { Path = path }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> WriteBackupFileContextManifestAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);

        var fullDirectory = files.ReadFullPathBackupFileContext(backupFileContext: new BackupFileContext { Path = backupFileContext.Directory })
            .TextResult!;

        var path = files.ReadCombinedPathBackupFileContext(backupFileContext: new BackupFileContext { Directory = fullDirectory, Name = "manifest.json" })
            .TextResult!;

        return await manifests.WriteBackupFileContextManifestAsync(backupFileContext: backupFileContext with { Path = path }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<BackupFileContext> ReadBackupFileContextVerificationAsync(BackupFileContext backupFileContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateBackupFileContextCancellation(backupFileContext: backupFileContext, cancellationToken: cancellationToken);
        var errors = new List<string>();
        BackupFileContext read;

        try
        {
            var directory = files.ReadFullPathBackupFileContext(backupFileContext: new BackupFileContext { Path = backupFileContext.Directory })
            .TextResult!;

            var path = files.ReadCombinedPathBackupFileContext(backupFileContext: new BackupFileContext { Directory = directory, Name = "manifest.json" })
            .TextResult!;

            read = await manifests.ReadBackupFileContextManifestAsync(backupFileContext: backupFileContext with { Path = path }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception exception)when (exception is System.IO.IOException or System.Text.Json.JsonException or StorageException)
        {
            return backupFileContext with
            {
                Verification = new BackupVerificationResult
                {
                    IsValid = false,
                    Errors = ["MANIFEST_INVALID:" + exception.Message]
                }
            };
        }

        foreach (var file in read.Manifest!.Files)
        {
            var name = files.ReadFileNameBackupFileContext(backupFileContext: new BackupFileContext { Path = file.Name })
            .TextResult;

            if (file.Name != name)
            {
                throw new StorageFormatException(message: "Backup file name escapes its directory.");
            }

            var fullDirectory = files.ReadFullPathBackupFileContext(backupFileContext: new BackupFileContext { Path = backupFileContext.Directory })
            .TextResult!;

            var path = files.ReadCombinedPathBackupFileContext(backupFileContext: new BackupFileContext { Directory = fullDirectory, Name = file.Name })
            .TextResult!;

            if (files.ReadLengthBackupFileContext(backupFileContext: new BackupFileContext { Path = path })
            .Length is null)
            {
                errors.Add(item: $"FILE_MISSING:{file.Name}");
                continue;
            }

            var actual = await files.DescribeBackupFileContextAsync(backupFileContext: new BackupFileContext { Path = path, Name = file.Name, Kind = file.Kind }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (actual.FileResult!.Size != file.Size)
            {
                errors.Add(item: $"SIZE_MISMATCH:{file.Name}");
            }

            if (!files.MatchChecksumBackupFileContext(backupFileContext: actual with { TextResult = actual.FileResult.Sha256, ExpectedSha256 = file.Sha256 })
            .ChecksumMatches)
            {
                errors.Add(item: $"CHECKSUM_MISMATCH:{file.Name}");
            }
        }

        return read with
        {
            Verification = new BackupVerificationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors.AsReadOnly()
            }
        };
    });
}