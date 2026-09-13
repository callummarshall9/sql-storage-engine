using System.Text.Json;

namespace sql_storage_engine.Transactions;

internal static class StorageTransactionFiles
{
    internal static string JournalPath(string path) => path + ".transaction-undo";
    internal static string ActivePath(string path) => path + ".transaction-active";
    private static string ReceiptPath(string path, Guid id) => path + $".transaction-{id:N}.receipt";

    internal static async ValueTask WriteAsync<T>(string path, T value, CancellationToken token)
    {
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, value, cancellationToken: token).ConfigureAwait(false);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
            TransactionDirectory.Flush(path);
        }
        finally { if (File.Exists(temporary)) TransactionDirectory.Delete(temporary); }
    }

    internal static async ValueTask<StorageTransactionReceipt> ResolveAsync(string path, StorageTransactionIdentity identity, CancellationToken token)
    {
        var receiptPath = ReceiptPath(path, identity.Value);
        if (!File.Exists(receiptPath)) return new(identity, StorageTransactionState.Indeterminate);
        await using var stream = File.OpenRead(receiptPath);
        var receipt = await JsonSerializer.DeserializeAsync<StorageTransactionReceipt>(stream, cancellationToken: token).ConfigureAwait(false);
        if (receipt is null || receipt.Identity != identity || receipt.State is not (StorageTransactionState.Committed or StorageTransactionState.Aborted))
            throw new InvalidDataException("Transaction receipt is invalid.");
        return receipt;
    }

    internal static ValueTask WriteReceiptAsync(string path, StorageTransactionReceipt receipt, CancellationToken token) =>
        WriteAsync(ReceiptPath(path, receipt.Identity.Value), receipt, token);

    internal static void ReleaseReceipt(string path, StorageTransactionIdentity identity) => TransactionDirectory.Delete(ReceiptPath(path, identity.Value));

    internal static async ValueTask RecoverAsync(string path, CancellationToken token)
    {
        var active = ActivePath(path);
        var journal = JournalPath(path);
        if (!File.Exists(active))
        {
            // A begin can fail before publishing its identity, but cannot run a callback before publication.
            if (File.Exists(journal)) await StatementJournal.RecoverIfNeededAsync(path, token, journal).ConfigureAwait(false);
            return;
        }
        StorageTransactionIdentity identity;
        await using (var stream = File.OpenRead(active))
            identity = await JsonSerializer.DeserializeAsync<StorageTransactionIdentity>(stream, cancellationToken: token).ConfigureAwait(false);
        if (identity.Value == Guid.Empty || identity.DatabaseId.Value == Guid.Empty) throw new InvalidDataException("Transaction identity is invalid.");
        var receipt = await ResolveAsync(path, identity, token).ConfigureAwait(false);
        if (File.Exists(journal))
        {
            if (await StatementJournal.ReadDatabaseIdentityAsync(journal, token).ConfigureAwait(false) != identity.DatabaseId)
                throw new InvalidDataException("Transaction journal belongs to another database.");
            var committed = await StatementJournal.IsCommittedAsync(journal, token).ConfigureAwait(false);
            if (receipt.State != StorageTransactionState.Indeterminate &&
                receipt.State != (committed ? StorageTransactionState.Committed : StorageTransactionState.Aborted))
                throw new InvalidDataException("Transaction decision contradicts its receipt.");
            // Recover the outer image before deleting a nested statement journal; never restore an intermediate root state.
            await StatementJournal.RecoverIfNeededAsync(path, token, journal, retain: true).ConfigureAwait(false);
            receipt = new(identity, committed ? StorageTransactionState.Committed : StorageTransactionState.Aborted);
            await WriteReceiptAsync(path, receipt, token).ConfigureAwait(false);
            TransactionDirectory.Delete(StatementJournal.GetPath(path));
            TransactionDirectory.Delete(journal);
        }
        else if (receipt.State == StorageTransactionState.Indeterminate)
            throw new InvalidDataException("An unresolved transaction has no recovery evidence.");
        TransactionDirectory.Delete(active);
    }
}
