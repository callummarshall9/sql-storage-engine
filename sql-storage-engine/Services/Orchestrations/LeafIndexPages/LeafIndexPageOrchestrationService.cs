// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers.Binary;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Foundations.PageHeaders;
using sql_storage_engine.Services.Foundations.IndexKeys;
using sql_storage_engine.Services.Foundations.LeafIndexEntries;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.LeafIndexPages;
internal sealed partial class LeafIndexPageOrchestrationService : ILeafIndexPageOrchestrationService
{
    private static readonly IPageHeaderService pageHeaders = new PageHeaderService();
    private static readonly IIndexKeyService indexKeys = new IndexKeyService();
    private static readonly ILeafIndexEntryService leafEntries = new LeafIndexEntryService();
    public bool CheckLeafIndexPageCapacity(int pageSize, LeafIndexPage leafIndexPage) =>
        TryCatch(operation: () =>
    {
        ValidateLeafIndexPageCapacity(pageSize: pageSize, leafIndexPage: leafIndexPage);
        return CanFitCore(pageSize: pageSize, entries: leafIndexPage.CapacityEntries ?? leafIndexPage.Entries);
    });

    public void WriteLeafIndexPage(Span<byte> page, LeafIndexPage leafIndexPage) =>
        TryCatch(operation: static context =>
    {
        ValidateLeafIndexPageOnWrite(page: context.page, leafIndexPage: context.leafIndexPage);
        WriteCore(page: context.page, model: context.leafIndexPage);
    }, context: new WritableLeafIndexPageContext { page = page, leafIndexPage = leafIndexPage });

    public LeafIndexPage ReadLeafIndexPage(ReadOnlySpan<byte> page, ulong expectedPageId) =>
        TryCatch(operation: static context =>
    {
        ValidateLeafIndexPageSource(page: context.page, expectedPageId: context.expectedPageId);
        return ReadCore(page: context.page, expectedPageId: new PageId { Value = context.expectedPageId });
    }, context: new ReadableLeafIndexPageContext { page = page, expectedPageId = expectedPageId });

    private static bool CanFitCore(int pageSize, IEnumerable<sql_storage_engine.Models.Indexes.LeafIndexEntry> entries)
    {
        if (!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(pageSize)))
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var values = entries.ToArray();

        var required = checked(LeafIndexPageFormat.HeaderLength + values.Length * LeafIndexPageFormat.SlotLength + values.Sum(selector: entry => indexKeys.ReadIndexKeyBytes(indexKey: entry.Key)
            .Length + leafEntries.ReadLeafIndexEntryPayload(leafIndexEntry: entry)
            .Length));

        return values.Length <= ushort.MaxValue && required <= pageSize;
    }

    private static void WriteCore(Span<byte> page, LeafIndexPage model)
    {
        ValidateModel(model: model);

        if (!CanFitCore(pageSize: page.Length, entries: model.Entries))
        {
            throw new ArgumentException("Leaf entries do not fit on the page.", nameof(model));
        }

        page.Clear();
        pageHeaders.WritePageHeader(destination: page, pageHeader: new PageHeader { PageId = model.PageId, PageType = PageType.BPlusTreeLeaf, FormatVersion = new sql_storage_engine.Models.Pages.PageFormatVersion { Value = sql_storage_engine.Models.Pages.PageHeaderFormat.CurrentVersion }, PageLogSequenceNumber = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 }, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32, Checksum = 0 });
        WriteOptionalPage(destination: page[LeafIndexPageFormat.ParentOffset..], pageId: model.ParentPageId);
        WriteOptionalPage(destination: page[LeafIndexPageFormat.PreviousOffset..], pageId: model.PreviousPageId);
        WriteOptionalPage(destination: page[LeafIndexPageFormat.NextOffset..], pageId: model.NextPageId);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: page[LeafIndexPageFormat.EntryCountOffset..], value: checked((ushort)model.Entries.Count));
        var directoryEnd = checked(LeafIndexPageFormat.HeaderLength + model.Entries.Count * LeafIndexPageFormat.SlotLength);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: page[LeafIndexPageFormat.SlotDirectoryEndOffset..], value: checked((uint)directoryEnd));
        page[69] = 1;
        var cursor = page.Length;

        for (var index = 0; index < model.Entries.Count; index++)
        {
            sql_storage_engine.Models.Indexes.LeafIndexEntry entry = model.Entries[index];

            var payload = leafEntries.ReadLeafIndexEntryPayload(leafIndexEntry: entry)
            .Span;

            var payloadOffset = 0;

            if (!payload.IsEmpty)
            {
                cursor = checked(cursor - payload.Length);
                payload.CopyTo(destination: page[cursor..]);
                payloadOffset = cursor;
            }

            var key = indexKeys.ReadIndexKeyBytes(indexKey: entry.Key)
            .Span;

            cursor = checked(cursor - key.Length);
            key.CopyTo(destination: page[cursor..]);
            var slot = page.Slice(start: LeafIndexPageFormat.HeaderLength + index * LeafIndexPageFormat.SlotLength, length: LeafIndexPageFormat.SlotLength);
            BinaryPrimitives.WriteUInt32LittleEndian(destination: slot, value: checked((uint)cursor));
            BinaryPrimitives.WriteUInt16LittleEndian(destination: slot[4..], value: checked((ushort)key.Length));
            BinaryPrimitives.WriteUInt16LittleEndian(destination: slot[6..], value: checked((ushort)payload.Length));
            BinaryPrimitives.WriteUInt64LittleEndian(destination: slot[8..], value: entry.RowId.PageId.Value);
            BinaryPrimitives.WriteUInt16LittleEndian(destination: slot[16..], value: entry.RowId.SlotId.Value);
            BinaryPrimitives.WriteUInt16LittleEndian(destination: slot[18..], value: checked((ushort)payloadOffset));
            BinaryPrimitives.WriteUInt32LittleEndian(destination: slot[20..], value: entry.RowId.Generation.Value);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination: page[LeafIndexPageFormat.KeyDataStartOffset..], value: checked((uint)cursor));
        pageHeaders.WriteChecksum(page: page, pageSize: page.Length);
    }

    private static LeafIndexPage ReadCore(ReadOnlySpan<byte> page, PageId expectedPageId)
    {
        if (!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(page.Length)))
        {
            throw new ArgumentException("Index page has an unsupported size.", nameof(page));
        }

        pageHeaders.ValidateChecksum(page: page, pageSize: page.Length);
        pageHeaders.CheckPageHeader(pageHeader: pageHeaders.ReadPageHeader(source: page), expectedPageId: (expectedPageId).Value, expectedType: PageType.BPlusTreeLeaf);
        var layout = page[69];

        if (layout > 1 || ContainsNonZero(bytes: page[70..LeafIndexPageFormat.HeaderLength]))
        {
            throw new StorageFormatException("Unknown leaf payload layout or nonzero reserved bytes.");
        }

        var parent = ReadOptionalPage(source: page[LeafIndexPageFormat.ParentOffset..]);
        var previous = ReadOptionalPage(source: page[LeafIndexPageFormat.PreviousOffset..]);
        var next = ReadOptionalPage(source: page[LeafIndexPageFormat.NextOffset..]);

        if (parent == expectedPageId || previous == expectedPageId || next == expectedPageId || previous is not null && previous == next)
        {
            throw new StorageCorruptionException("Leaf page links are self-referential or ambiguous.");
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(source: page[LeafIndexPageFormat.EntryCountOffset..]);
        var directoryEnd = BinaryPrimitives.ReadUInt32LittleEndian(source: page[LeafIndexPageFormat.SlotDirectoryEndOffset..]);
        var expectedDirectoryEnd = checked((uint)(LeafIndexPageFormat.HeaderLength + count * LeafIndexPageFormat.SlotLength));
        var keyStart = BinaryPrimitives.ReadUInt32LittleEndian(source: page[LeafIndexPageFormat.KeyDataStartOffset..]);

        if (directoryEnd != expectedDirectoryEnd || directoryEnd > keyStart || keyStart > page.Length)
        {
            throw new StorageCorruptionException("Leaf slot and key regions overlap or exceed the page.");
        }

        List<sql_storage_engine.Models.Indexes.LeafIndexEntry> entries = [];
        var expectedKeyEnd = page.Length;

        for (var index = 0; index < count; index++)
        {
            var slot = page.Slice(start: LeafIndexPageFormat.HeaderLength + index * LeafIndexPageFormat.SlotLength, length: LeafIndexPageFormat.SlotLength);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(source: slot);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(source: slot[4..]);
            var payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(source: slot[6..]);
            var payloadOffset = BinaryPrimitives.ReadUInt16LittleEndian(source: slot[18..]);

            if (layout == 0 && (payloadLength != 0 || payloadOffset != 0))
            {
                throw new StorageFormatException("Legacy leaf slots contain nonzero reserved bytes.");
            }

            var payloadEnd = payloadLength == 0 ? expectedKeyEnd : checked(payloadOffset + payloadLength);

            if (payloadLength != 0 && (payloadOffset < keyStart || payloadEnd != expectedKeyEnd))
            {
                throw new StorageCorruptionException("Leaf included payload offset is malformed.");
            }

            var expectedKeyBoundary = payloadLength == 0 ? expectedKeyEnd : payloadOffset;

            if (length == 0 || checked((ulong)offset + length) > (ulong)page.Length || offset < keyStart || checked((int)(offset + length)) != expectedKeyBoundary)
            {
                throw new StorageCorruptionException("Leaf key offset is malformed.");
            }

            sql_storage_engine.Models.Indexes.IndexKey key = indexKeys.CreateIndexKey(bytes: page.Slice(start: checked((int)offset), length: length));

            if (entries.Count > 0 && indexKeys.CompareIndexKeys(indexKeys: [entries[^1].Key, key]) > 0)
            {
                throw new StorageCorruptionException("Leaf entries are not key ordered.");
            }

            var rowPageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: slot[8..])
            };

            if (rowPageId.Value == 0)
            {
                throw new StorageCorruptionException("Leaf RowId cannot reference page zero.");
            }

            entries.Add(item: leafEntries.CreateLeafIndexEntry(leafIndexEntry: new sql_storage_engine.Models.Indexes.LeafIndexEntry { Key = key, RowId = new sql_storage_engine.Models.Identifiers.RowId { PageId = rowPageId, SlotId = new sql_storage_engine.Models.Identifiers.SlotId { Value = BinaryPrimitives.ReadUInt16LittleEndian(source: slot[16..]) }, Generation = new sql_storage_engine.Models.Identifiers.SlotGeneration { Value = BinaryPrimitives.ReadUInt32LittleEndian(source: slot[20..]) } } }, includedPayload: (payloadLength == 0 ? ReadOnlyMemory<byte>.Empty : page.Slice(start: payloadOffset, length: payloadLength)
            .ToArray()).Span));

            expectedKeyEnd = checked((int)offset);
        }

        if (expectedKeyEnd != keyStart)
        {
            throw new StorageCorruptionException("Leaf key packing is invalid.");
        }

        var model = new LeafIndexPage
        {
            PageId = expectedPageId,
            ParentPageId = parent,
            PreviousPageId = previous,
            NextPageId = next,
            Entries = entries.AsReadOnly()
        };

        ValidateModel(model: model);
        return model;
    }

    private static void WriteOptionalPage(Span<byte> destination, PageId? pageId)
    {
        destination[0] = pageId.HasValue ? (byte)1 : (byte)0;

        BinaryPrimitives.WriteUInt64LittleEndian(destination: destination[1..], value: pageId.GetValueOrDefault()
            .Value);
    }

    private static PageId? ReadOptionalPage(ReadOnlySpan<byte> source) =>
        source[0] switch
    {
        0 when BinaryPrimitives.ReadUInt64LittleEndian(source: source[1..]) == 0 => null,
        1 => new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = BinaryPrimitives.ReadUInt64LittleEndian(source: source[1..])
        },
        _ => throw new StorageFormatException("Invalid nullable leaf link encoding.")};

    private static bool ContainsNonZero(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            if (value != 0)
            {
                return true;
            }
        }

        return false;
    }

    public PageType ReadLeafIndexPageType(ReadOnlySpan<byte> page) =>
        TryCatch(operation: static context =>
    {
        ValidateLeafIndexPageTypeSource(page: context.page);

        return pageHeaders.ReadPageHeader(source: context.page)
            .PageType;
    }, context: new ReadableLeafIndexPageContext { page = page });
}