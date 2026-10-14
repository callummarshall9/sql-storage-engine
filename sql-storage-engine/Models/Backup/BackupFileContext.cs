// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Backup;
internal sealed record BackupFileContext
{
    public string? Path { get; init; }
    public string? Directory { get; init; }
    public string? Name { get; init; }
    public string? Kind { get; init; }
    public string? SourcePath { get; init; }
    public string? DestinationPath { get; init; }
    public string? TextResult { get; init; }
    public byte[]? Bytes { get; init; }
    public BackupManifest? Manifest { get; init; }
    public BackupFile? FileResult { get; init; }
    public bool Exists { get; init; }
    public long? Length { get; init; }
    public System.DateTimeOffset CreatedAt { get; init; }
    public ulong PageId { get; init; }
    public int PageSize { get; init; }
    public BackupConfiguration? Configuration { get; init; }
    public BackupTimeline? Timeline { get; init; }
    public string? ExpectedSha256 { get; init; }
    public bool ChecksumMatches { get; init; }
    public BackupVerificationResult? Verification { get; init; }
    public string? DatabasePath { get; init; }
    public System.Collections.Generic.IEnumerable<string>? WalPaths { get; init; }
    public string? DestinationDirectory { get; init; }
    public bool RequireCleanShutdown { get; init; }
    public sql_storage_engine.Models.Identifiers.LogSequenceNumber? StartLsn { get; init; }
    public sql_storage_engine.Models.Identifiers.LogSequenceNumber? EndLsn { get; init; }
    public sql_storage_engine.Models.Identifiers.LogSequenceNumber TargetLsn { get; init; }
    public System.Collections.Generic.IEnumerable<string>? ArchivedPaths { get; init; }
    public sql_storage_engine.Models.Logging.WalSegmentHeader? SegmentHeader { get; init; }
    public sql_storage_engine.Models.Logging.WalRecord[]? Records { get; init; }
    public PointInTimeRestoreResult? PointInTimeResult { get; init; }
    public ulong? RetentionRegistrationId { get; init; }
    public System.Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask>? AfterStart { get; init; }
    public System.Collections.Generic.IEnumerable<sql_storage_engine.Models.Logging.WalRecord>? RecordSource { get; init; }
    public sql_storage_engine.Models.Identifiers.LogSequenceNumber CurrentLsn { get; init; }
}