using System.Buffers.Binary;
using System.Text.Json;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Backup;

public sealed class WrongDatabaseWalException(string message) : StorageException(message);
public sealed class MissingWalSegmentException(string message) : StorageException(message);
public sealed record PointInTimeRestoreResult(string DatabasePath, ulong Timeline,
    LogSequenceNumber ReplayedThrough, IReadOnlyList<TransactionId> CommittedTransactions);

/// <summary>Restores a verified base and selects committed archived WAL records through an exact target LSN.</summary>
public sealed class PointInTimeRestore(OfflineBackupManager backupManager)
{
    public async Task<PointInTimeRestoreResult> RestoreAsync(string backupDirectory,
        IEnumerable<string> archivedSegmentPaths, LogSequenceNumber targetLsn, string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        if (targetLsn.Value == 0) throw new ArgumentOutOfRangeException(nameof(targetLsn));
        var manifest = await backupManager.ReadManifestAsync(backupDirectory, cancellationToken).ConfigureAwait(false);
        var segments = new List<(WalSegmentHeader Header, IReadOnlyList<WalRecord> Records)>();
        foreach (var path in archivedSegmentPaths)
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (bytes.Length < WalFormat.SegmentHeaderLength) throw new StorageFormatException("Archived WAL segment is truncated.");
            var header = WalFormat.ReadSegmentHeader(bytes);
            if (header.DatabaseId != manifest.DatabaseId)
                throw new WrongDatabaseWalException("Archived WAL belongs to a different database.");
            segments.Add((header, WalFormat.ReadRecords(bytes.AsSpan(WalFormat.SegmentHeaderLength)).Records));
        }
        if (segments.Count == 0) throw new MissingWalSegmentException("No archived WAL segments were supplied.");
        segments.Sort((left, right) => left.Header.SegmentNumber.CompareTo(right.Header.SegmentNumber));
        var timeline = segments[0].Header.Timeline;
        for (var index = 0; index < segments.Count; index++)
        {
            if (segments[index].Header.Timeline != timeline ||
                index > 0 && segments[index].Header.SegmentNumber != segments[index - 1].Header.SegmentNumber + 1)
                throw new MissingWalSegmentException("Archived WAL timeline has a missing or mismatched segment.");
        }
        var selected = segments.SelectMany(segment => segment.Records).Where(record => record.Lsn.Value <= targetLsn.Value)
            .OrderBy(record => record.Lsn.Value).ToArray();
        if (selected.Length == 0 || selected[^1].Lsn.Value < targetLsn.Value)
            throw new MissingWalSegmentException("Archived WAL does not reach the requested target LSN.");
        var committed = selected.Where(record => record.Type == WalRecordType.Commit)
            .Select(record => record.TransactionId).Distinct().OrderBy(id => id.Value).ToArray();
        var databasePath = await backupManager.RestoreAsync(backupDirectory, destinationDirectory, cancellationToken)
            .ConfigureAwait(false);
        var newTimeline = checked(timeline + 1);
        await File.WriteAllBytesAsync(Path.Combine(destinationDirectory, "timeline.json"),
            JsonSerializer.SerializeToUtf8Bytes(new { DatabaseId = manifest.DatabaseId.Value, Timeline = newTimeline,
                ParentTimeline = timeline, ForkLsn = targetLsn.Value }), cancellationToken).ConfigureAwait(false);
        return new PointInTimeRestoreResult(databasePath, newTimeline, targetLsn, committed);
    }

    public static byte[] CreateArchiveSegment(WalSegmentHeader header, IEnumerable<WalRecord> records)
    {
        var encoded = records.Select(WalFormat.WriteRecord).ToArray();
        var result = new byte[checked(WalFormat.SegmentHeaderLength + encoded.Sum(record => record.Length))];
        WalFormat.WriteSegmentHeader(header).CopyTo(result, 0);
        var offset = WalFormat.SegmentHeaderLength;
        foreach (var record in encoded) { record.CopyTo(result, offset); offset += record.Length; }
        return result;
    }
}
