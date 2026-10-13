// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.Models.Pages;
public readonly record struct PageHeader
{
    public PageId PageId { get; init; }
    public PageType PageType { get; init; }
    public PageFormatVersion FormatVersion { get; init; }
    public LogSequenceNumber PageLogSequenceNumber { get; init; }
    public PageChecksumAlgorithm ChecksumAlgorithm { get; init; }
    public uint Checksum { get; init; }
}