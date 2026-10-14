// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Access;
using sql_storage_engine.Models.Buffers;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Models.Storage;
internal sealed record EngineResourceContext
{
    public PageDatabaseContext? Database { get; set; }
    public PageDatabaseBinding? DatabaseBinding { get; set; }
    public BufferPoolConfiguration? BufferConfiguration { get; set; }
    public object? BufferSource { get; set; }
    public StorageAccess? Access { get; set; }
    public IDisposable? Lifetime { get; set; }
    public IAsyncDisposable? ScopeResource { get; init; }
    public StorageFileLeaseConfiguration? FileLeaseConfiguration { get; init; }
    public StatementJournalState? Journal { get; set; }
    public DatabaseHeader? Header { get; set; }
    public string? Path { get; init; }
    public string? ExplicitPath { get; init; }
    public bool Creating { get; init; }
    public bool Reentrant { get; init; }
    public bool Retain { get; init; }
    public int PageSize { get; init; }
    public int BufferCapacity { get; init; }
    public long? MaximumLength { get; init; }
    public long Length { get; init; }
    public ulong PageId { get; init; }
    public byte[]? Bytes { get; set; }
    public byte SnapshotState { get; set; }
    public Guid DatabaseIdentity { get; set; }
    public StorageTransactionReceipt? Receipt { get; set; }
    public object? Metadata { get; init; }
    public string? TextResult { get; set; }
    public bool Exists { get; set; }
    public Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask>? WritePage { get; init; }
    public Action<long>? RestoreLength { get; init; }
    public Func<CancellationToken, ValueTask>? Flush { get; init; }
    public Func<CancellationToken, ValueTask>? ReloadHeader { get; init; }
    public object? StreamSource { get; set; }
    public sql_storage_engine.Models.Catalog.CatalogStorageConfiguration? CatalogStorage { get; set; }
    public sql_storage_engine.Models.Overflow.OverflowPageResourceConfiguration? OverflowConfiguration { get; set; }
    public Func<ulong, CancellationToken, ValueTask<(IDisposable Lease, Memory<byte> Memory, Action<ulong> MarkDirty)>>? PinSource { get; set; }
    public Func<ulong, CancellationToken, ValueTask>? DiscardSource { get; set; }
    public Func<CancellationToken, ValueTask>? BufferFlushSource { get; set; }
    public object? RuntimeSource { get; set; }
    public TimeSpan Timeout { get; init; }
    public Action? ExpiryNotification { get; init; }
    public CancellationToken InputToken { get; init; }
    public CancellationToken TokenResult { get; set; }
    public Task? TaskSource { get; init; }
}