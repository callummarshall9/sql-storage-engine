// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.PageHeaders;
internal sealed partial class PageHeaderService
{
    private static void ValidatePageHeaderOnWrite(Span<byte> destination, PageHeader pageHeader)
    {
        Validate(inputs: [(destination.Length < PageHeaderFormat.EncodedLength, "Page header destination is truncated.")]);
        ValidatePageHeader(pageHeader: pageHeader, expectedPageId: pageHeader.PageId.Value, expectedType: null);
    }

    private static void ValidatePageHeaderSource(ReadOnlySpan<byte> source)
    {
        Validate(inputs: [(source.Length < PageHeaderFormat.EncodedLength, "Page header is truncated; expected 32 bytes.")]);
        Validate(inputs: [(source[22..PageHeaderFormat.ChecksumOffset].ContainsAnyExcept(value: (byte)0), "Reserved page-pageHeader bytes must be zero.")]);
    }

    private static void ValidatePageHeader(PageHeader pageHeader, ulong expectedPageId, PageType? expectedType)
    {
        if (pageHeader.PageId.Value != expectedPageId)
        {
            throw new StorageCorruptionException(message: $"Expected page {expectedPageId}, found {pageHeader.PageId.Value}.");
        }

        Validate(inputs: [(!Enum.IsDefined(value: pageHeader.PageType) || pageHeader.PageType == PageType.Unknown, $"Unsupported page type value {(ushort)pageHeader.PageType}."), (expectedType is not null && pageHeader.PageType != expectedType, $"Expected page type {expectedType}, found {pageHeader.PageType}."), (pageHeader.FormatVersion.Value != PageHeaderFormat.CurrentVersion, $"Unsupported page format version {pageHeader.FormatVersion.Value}."), (pageHeader.ChecksumAlgorithm != PageChecksumAlgorithm.Crc32, $"Unsupported checksum algorithm {(ushort)pageHeader.ChecksumAlgorithm}.")]);
    }

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new StorageFormatException(message: input.Message);
            }
        }
    }

    private static void ValidatePageSize(ReadOnlySpan<byte> page, int pageSize) =>
        ValidateChecksumInputs(inputs: [(page.Length != pageSize || !System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(pageSize)), pageSize)]);

    private static void ValidateChecksumInputs(ReadOnlySpan<(bool Invalid, int PageSize)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: $"Page must be exactly a supported {input.PageSize}-byte page.");
            }
        }
    }
}