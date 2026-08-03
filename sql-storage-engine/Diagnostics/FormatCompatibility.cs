using System.Text.Json;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Diagnostics;

public sealed record CompatibilityFixture(string Name, ushort FormatVersion, Guid DatabaseId, int PageSize,
    ulong Timeline, IReadOnlyList<string> ExpectedRows, IReadOnlyList<CompatibilityWalRecord> WalRecords);
public sealed record CompatibilityWalRecord(ulong Lsn, ulong TransactionId, string Type);
public sealed record CompatibilityResult(DatabaseHeader Header, IntegrityReport Integrity,
    IReadOnlyList<string> Rows, IReadOnlyList<WalRecord> WalRecords);

/// <summary>Loads bounded released-format fixtures and validates their materialized database and WAL.</summary>
public static class FormatCompatibility
{
    public const int MaximumFixtureBytes = 1024 * 1024;
    public static async Task<CompatibilityResult> OpenAsync(string fixturePath, string materializedDatabasePath,
        CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(fixturePath);
        if (!info.Exists || info.Length is <= 0 or > MaximumFixtureBytes) throw new StorageFormatException("Compatibility fixture size is invalid.");
        var bytes = await File.ReadAllBytesAsync(fixturePath, cancellationToken).ConfigureAwait(false);
        var fixture = JsonSerializer.Deserialize<CompatibilityFixture>(bytes) ?? throw new StorageFormatException("Compatibility fixture is empty.");
        if (fixture.FormatVersion != DatabaseHeader.CurrentFormatVersion)
            throw new UnsupportedDatabaseVersionException(fixture.FormatVersion);
        if (!PageConstants.IsSupportedSize(fixture.PageSize) || fixture.Timeline == 0)
            throw new StorageFormatException("Compatibility fixture metadata is invalid.");
        var header = new DatabaseHeader(new DatabaseId(fixture.DatabaseId), fixture.PageSize, fixture.FormatVersion,
            null, null, new TableId(1), new IndexId(1), new TransactionId(1), new PageId(1), true);
        var page = new byte[fixture.PageSize]; DatabaseHeaderCodec.Write(page, header);
        await File.WriteAllBytesAsync(materializedDatabasePath, page, cancellationToken).ConfigureAwait(false);
        await using var database = await PageDatabase.OpenAsync(materializedDatabasePath, DatabaseOpenMode.ReadOnly, cancellationToken).ConfigureAwait(false);
        var integrity = await new DatabaseIntegrityChecker().CheckAsync(database, database.Header, cancellationToken: cancellationToken).ConfigureAwait(false);
        var walRecords = fixture.WalRecords.Select(record => new WalRecord(new LogSequenceNumber(record.Lsn), default,
            new TransactionId(record.TransactionId), Enum.Parse<WalRecordType>(record.Type), ReadOnlyMemory<byte>.Empty)).ToArray();
        var encodedWal = walRecords.SelectMany(WalFormat.WriteRecord).ToArray();
        var decodedWal = WalFormat.ReadRecords(encodedWal).Records;
        return new CompatibilityResult(database.Header, integrity, fixture.ExpectedRows.ToArray(), decodedWal);
    }
}
