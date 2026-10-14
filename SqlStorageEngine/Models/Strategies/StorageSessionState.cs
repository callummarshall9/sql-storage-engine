// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.Models.Strategies;
internal sealed class StorageSessionState
{
    public bool Remote { get; set; }
    public bool Disposed { get; set; }
    public bool Conflicted { get; set; }
    public string? Name { get; set; }
    public string? ManifestKey { get; set; }
    public Guid Runtime { get; set; }
    public StorageStrategyOptions? Options { get; set; }
    public PageDatabaseContext? Database { get; set; }
    public DatabaseHeader? Header { get; set; }
    public StorageObjectManifest? Manifest { get; set; }
    public string? MetadataVersion { get; set; }
    public Dictionary<int, byte[]>? Cache { get; set; }
    public Dictionary<int, long>? Accesses { get; set; }
    public HashSet<int>? Dirty { get; set; }
    public HashSet<ulong>? DirtyPages { get; set; }
    public long Clock { get; set; }
    public long CacheBytes { get; set; }
    public long CommittedGeneration { get; set; }
    public StorageObjectManifest? PendingManifest { get; set; }
    public byte[]? PendingManifestBytes { get; set; }
    public (string Key, byte[] Bytes)[]? PendingUploads { get; set; }
    public string? LastError { get; set; }
    public int PageSize { get; set; }
}