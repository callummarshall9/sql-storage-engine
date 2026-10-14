// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Services.Foundations.TransactionDirectories;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.StatementJournals;
internal sealed partial class TransactionDirectoryStatementJournalProcessingService : ITransactionDirectoryStatementJournalProcessingService
{
    private readonly TransactionDirectoryService directories;
    public TransactionDirectoryStatementJournalProcessingService(TransactionDirectoryService transactionDirectoryService)
    {
        directories = transactionDirectoryService;
    }

    public string ReadStatementJournalStatePath(string databasePath) =>
        TryCatch(operation: () =>
    {
        ValidateDatabasePath(databasePath: databasePath);
        return JournalPath(databasePath: databasePath);
    });

    public ValueTask<StatementJournalState> CreateStatementJournalStateAsync(string databasePath, CancellationToken cancellationToken, string? explicitPath = null) =>
        TryCatch(operation: async () =>
    {
        ValidateJournalPaths(databasePath: databasePath, cancellationToken: cancellationToken, explicitPath: explicitPath);
        var journalPath = explicitPath ?? JournalPath(databasePath: databasePath);

        if (directories.ExistsTransactionFile(path: journalPath))
        {
            throw CreateStorageResourceException();
        }

        await directories.CaptureTransactionSnapshotAsync(databasePath: databasePath, snapshotPath: journalPath, headerFactory: CreateHeader, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new StatementJournalState
        {
            Path = journalPath
        };
    });

    public ValueTask CommitStatementJournalStateAsync(StatementJournalState statementJournalState, CancellationToken cancellationToken, bool retain = false) =>
        TryCatch(operation: async () =>
    {
        ValidateJournalOperation(statementJournalState: statementJournalState, cancellationToken: cancellationToken, retain: retain);

        await directories.WriteTransactionSnapshotStateAsync(path: statementJournalState.Path, offset: 8, state: 2, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        statementJournalState.Completed = !retain;

        if (!retain)
        {
            directories.DeleteTransactionFile(path: statementJournalState.Path);
        }
    });

    public ValueTask RestoreStatementJournalStateAsync(StatementJournalState statementJournalState, int pageSize, Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask> writePage, Action<long> restoreLength, Func<CancellationToken, ValueTask> flush, Func<CancellationToken, ValueTask> reloadHeader, CancellationToken cancellationToken, bool retain = false) =>
        TryCatch(operation: async () =>
    {
        ValidateRestore(statementJournalState: statementJournalState, pageSize: pageSize, writePage: writePage, restoreLength: restoreLength, flush: flush, reloadHeader: reloadHeader, cancellationToken: cancellationToken, retain: retain);

        var snapshot = await ReadSnapshotAsync(path: statementJournalState.Path, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (snapshot.State != 1)
        {
            throw new StorageFormatException(message: "Only an active statement journal can be restored.");
        }

        if (snapshot.Bytes.LongLength % pageSize != 0)
        {
            throw new StorageFormatException(message: "Statement journal is not page aligned.");
        }

        for (var offset = 0; offset < snapshot.Bytes.Length; offset += pageSize)
        {
            await writePage(arg1: checked((ulong)(offset / pageSize)), arg2: snapshot.Bytes.AsMemory(start: offset, length: pageSize), arg3: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        restoreLength(obj: snapshot.Bytes.LongLength);

        await flush(arg: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await reloadHeader(arg: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        statementJournalState.Completed = !retain;

        if (!retain)
        {
            directories.DeleteTransactionFile(path: statementJournalState.Path);
        }
    });

    public ValueTask RecoverStatementJournalStateAsync(string databasePath, CancellationToken cancellationToken, string? explicitPath = null, bool retain = false) =>
        TryCatch(operation: async () =>
    {
        ValidateRecovery(databasePath: databasePath, cancellationToken: cancellationToken, explicitPath: explicitPath, retain: retain);
        var path = explicitPath ?? JournalPath(databasePath: databasePath);

        if (!directories.ExistsTransactionFile(path: path))
        {
            return;
        }

        var snapshot = await ReadSnapshotAsync(path: path, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (snapshot.State == 1)
        {
            await directories.PublishTransactionSnapshotBytesAsync(path: databasePath, bytes: snapshot.Bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        if (!retain)
        {
            directories.DeleteTransactionFile(path: path);
        }
    });

    public ValueTask<(byte State, byte[] Bytes)> ReadStatementJournalStateSnapshotAsync(string path, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateSnapshotPath(path: path, cancellationToken: cancellationToken);

        return await ReadSnapshotAsync(path: path, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void RemoveStatementJournalState(StatementJournalState statementJournalState) =>
        TryCatch(operation: () =>
    {
        ValidateStatementJournalState(statementJournalState: statementJournalState);
        directories.DeleteTransactionFile(path: statementJournalState.Path);
        statementJournalState.Completed = true;
    });

    public ValueTask ReleaseStatementJournalStateAsync(StatementJournalState statementJournalState) =>
        TryCatch(operation: async () =>
    {
        ValidateStatementJournalState(statementJournalState: statementJournalState);

        if (statementJournalState.Completed && directories.ExistsTransactionFile(path: statementJournalState.Path))
        {
            directories.DeleteTransactionFile(path: statementJournalState.Path);
        }

        await ValueTask.CompletedTask.ConfigureAwait(continueOnCapturedContext: false);
    });

    private static string JournalPath(string databasePath) =>
        databasePath + ".statement-undo";

    private static byte[] CreateHeader(long length, byte[] digest)
    {
        var header = new byte[64];
        "SQLSTMT1"u8.CopyTo(destination: header);
        header[8] = 1;
        BinaryPrimitives.WriteInt64LittleEndian(destination: header.AsSpan(start: 16), value: length);
        digest.CopyTo(array: header, index: 24);
        return header;
    }

    private async ValueTask<(byte State, byte[] Bytes)> ReadSnapshotAsync(string path, CancellationToken cancellationToken)
    {
        var source = await directories.ReadTransactionSnapshotBytesAsync(path: path, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (source.Length < 64 || !source.AsSpan(start: 0, length: 8)
            .SequenceEqual(other: "SQLSTMT1"u8))
        {
            throw new StorageFormatException(message: "Statement journal header is invalid.");
        }

        var state = source[8];

        if (state is not (1 or 2))
        {
            throw new StorageFormatException(message: "Statement journal state is invalid.");
        }

        var length = BinaryPrimitives.ReadInt64LittleEndian(source: source.AsSpan(start: 16));

        if (length < 0 || length != source.LongLength - 64 || length > int.MaxValue)
        {
            throw new StorageFormatException(message: "Statement journal length is invalid.");
        }

        var bytes = source.AsSpan(start: 64)
            .ToArray();

        if (!directories.HashTransactionSnapshotBytes(bytes: bytes)
            .AsSpan()
            .SequenceEqual(other: source.AsSpan(start: 24, length: 32)))
        {
            throw new StorageCorruptionException(message: "Statement journal checksum is invalid.");
        }

        return (state, bytes);
    }

    public ValueTask WriteStatementJournalStateMetadataAsync<T>(string path, T value, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStatementJournalMetadata(path: path, value: value, cancellationToken: cancellationToken);

        await directories.WriteTransactionMetadataAsync(path: path, value: value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<(Guid Value, Guid DatabaseId)> ReadStatementJournalStateIdentityAsync(string path, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateSnapshotPath(path: path, cancellationToken: cancellationToken);

        var identity = await directories.GetTransactionMetadataAsync<sql_storage_engine.Models.StorageTransactionIdentity>(path: path, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return (identity.Value, identity.DatabaseId.Value);
    });

    public bool CheckStatementJournalStateFile(string path) =>
        TryCatch(operation: () =>
    {
        ValidateJournalFile(path: path);
        return directories.ExistsTransactionFile(path: path);
    });

    public void DeleteStatementJournalStateFile(string path) =>
        TryCatch(operation: () =>
    {
        ValidateStatementJournalStateFileOnDelete(path: path);
        directories.DeleteTransactionFile(path: path);
    });

    public long ReadStatementJournalStateFileLength(string path) =>
        TryCatch(operation: () =>
    {
        ValidateJournalFile(path: path);
        return directories.ReadTransactionSnapshotFileLength(path: path);
    });

    public ValueTask RestoreStatementJournalStateOwnerAsync(StatementJournalState statementJournalState, Func<StatementJournalState, CancellationToken, ValueTask> restore) =>
        TryCatch(operation: async () =>
    {
        ValidateOwnerRestore(statementJournalState: statementJournalState, restore: restore);

        await directories.RunTransactionSnapshotRecoveryAsync(operation: token => restore(arg1: statementJournalState, arg2: token))
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    internal static TransactionDirectoryStatementJournalProcessingService CreateTransactionDirectoryStatementJournalProcessingService() =>
        new TransactionDirectoryStatementJournalProcessingService(transactionDirectoryService: sql_storage_engine.Services.Foundations.TransactionDirectories.TransactionDirectoryService.CreateTransactionDirectoryService());
}