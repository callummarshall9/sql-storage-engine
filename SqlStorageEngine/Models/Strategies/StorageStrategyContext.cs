// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;

namespace sql_storage_engine.Models.Strategies;
internal sealed record StorageStrategyContext
{
    public object? Service { get; set; }
    public int Operation { get; set; }
    public string? Name { get; set; }
    public int PageSize { get; set; }
    public StorageStrategyOptions? Options { get; set; }
    public StorageObjectResourceConfiguration? Objects { get; set; }
    public StorageSessionState? State { get; set; }
    public StorageObjectManifest? Manifest { get; set; }
    public byte[]? Bytes { get; set; }
    public string? MetadataVersion { get; set; }
    public string? Key { get; set; }
    public int MaximumBytes { get; set; }
    public ulong PageId { get; set; }
    public PageType PageType { get; set; }
    public Memory<byte> Destination { get; set; }
    public ReadOnlyMemory<byte> Source { get; set; }
    public DatabaseHeader? Header { get; set; }
    public StorageCommitStatus? Status { get; set; }
    public Guid Runtime { get; set; }
    public int SegmentIndex { get; set; }
    public (string Key, byte[] Bytes)[]? Uploads { get; set; }
}