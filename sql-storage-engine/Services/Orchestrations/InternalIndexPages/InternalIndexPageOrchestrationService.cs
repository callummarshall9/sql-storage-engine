// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers.Binary;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Foundations.PageHeaders;
using sql_storage_engine.Services.Foundations.IndexKeys;

namespace sql_storage_engine.Services.Orchestrations.InternalIndexPages;
internal sealed partial class InternalIndexPageOrchestrationService : IInternalIndexPageOrchestrationService
{
    private static readonly IPageHeaderService pageHeaders = new PageHeaderService();
    private static readonly IIndexKeyService indexKeys = new IndexKeyService();
    public bool CheckInternalIndexPageCapacity(int pageSize, InternalIndexPage internalIndexPage) =>
        TryCatch(operation: () =>
    {
        ValidateInternalIndexPageCapacity(pageSize: pageSize, internalIndexPage: internalIndexPage);
        return CanFitCore(pageSize: pageSize, separators: internalIndexPage.CapacitySeparators ?? internalIndexPage.Separators);
    });

    public void WriteInternalIndexPage(Span<byte> page, InternalIndexPage internalIndexPage) =>
        TryCatch(operation: static context =>
    {
        ValidateInternalIndexPageOnWrite(page: context.page, internalIndexPage: context.internalIndexPage);
        WriteCore(page: context.page, model: context.internalIndexPage);
    }, context: new WritableInternalIndexPageContext { page = page, internalIndexPage = internalIndexPage });

    public InternalIndexPage ReadInternalIndexPage(ReadOnlySpan<byte> page, ulong expectedPageId) =>
        TryCatch(operation: static context =>
    {
        ValidateInternalIndexPageSource(page: context.page, expectedPageId: context.expectedPageId);
        return ReadCore(page: context.page, expectedPageId: new PageId { Value = context.expectedPageId });
    }, context: new ReadableInternalIndexPageContext { page = page, expectedPageId = expectedPageId });

    private static bool CanFitCore(int pageSize, IEnumerable<sql_storage_engine.Models.Indexes.IndexKey> separators)
    {
        if (!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(pageSize)))
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var keys = separators.ToArray();

        var required = checked(InternalIndexPageFormat.HeaderLength + keys.Length * InternalIndexPageFormat.SlotLength + keys.Sum(selector: key => indexKeys.ReadIndexKeyBytes(indexKey: key)
            .Length));

        return keys.Length is> 0 and <= ushort.MaxValue && required <= pageSize;
    }

    private static void WriteCore(Span<byte> page, InternalIndexPage model)
    {
        ValidateModel(model: model);

        if (!CanFitCore(pageSize: page.Length, separators: model.Separators))
        {
            throw new ArgumentException("Internal node does not fit on the page.", nameof(model));
        }

        page.Clear();
        pageHeaders.WritePageHeader(destination: page, pageHeader: new PageHeader { PageId = model.PageId, PageType = PageType.BPlusTreeInternal, FormatVersion = new sql_storage_engine.Models.Pages.PageFormatVersion { Value = sql_storage_engine.Models.Pages.PageHeaderFormat.CurrentVersion }, PageLogSequenceNumber = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 }, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32, Checksum = 0 });
        WriteOptionalPage(destination: page[InternalIndexPageFormat.ParentOffset..], pageId: model.ParentPageId);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: page[InternalIndexPageFormat.SeparatorCountOffset..], value: checked((ushort)model.Separators.Count));
        BinaryPrimitives.WriteUInt16LittleEndian(destination: page[InternalIndexPageFormat.ChildCountOffset..], value: checked((ushort)model.Children.Count));
        var directoryEnd = checked(InternalIndexPageFormat.HeaderLength + model.Separators.Count * InternalIndexPageFormat.SlotLength);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: page[InternalIndexPageFormat.SlotDirectoryEndOffset..], value: checked((uint)directoryEnd));
        BinaryPrimitives.WriteUInt64LittleEndian(destination: page[InternalIndexPageFormat.FirstChildOffset..], value: model.Children[0].Value);
        var cursor = page.Length;

        for (var index = 0; index < model.Separators.Count; index++)
        {
            var key = indexKeys.ReadIndexKeyBytes(indexKey: model.Separators[index])
            .Span;

            cursor = checked(cursor - key.Length);
            key.CopyTo(destination: page[cursor..]);
            var slot = page.Slice(start: InternalIndexPageFormat.HeaderLength + index * InternalIndexPageFormat.SlotLength, length: InternalIndexPageFormat.SlotLength);
            BinaryPrimitives.WriteUInt32LittleEndian(destination: slot, value: checked((uint)cursor));
            BinaryPrimitives.WriteUInt16LittleEndian(destination: slot[4..], value: checked((ushort)key.Length));
            BinaryPrimitives.WriteUInt64LittleEndian(destination: slot[8..], value: model.Children[index + 1].Value);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination: page[InternalIndexPageFormat.KeyDataStartOffset..], value: checked((uint)cursor));
        pageHeaders.WriteChecksum(page: page, pageSize: page.Length);
    }

    private static InternalIndexPage ReadCore(ReadOnlySpan<byte> page, PageId expectedPageId)
    {
        if (!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(page.Length)))
        {
            throw new ArgumentException("Index page has an unsupported size.", nameof(page));
        }

        pageHeaders.ValidateChecksum(page: page, pageSize: page.Length);
        pageHeaders.CheckPageHeader(pageHeader: pageHeaders.ReadPageHeader(source: page), expectedPageId: (expectedPageId).Value, expectedType: PageType.BPlusTreeInternal);

        if (ContainsNonZero(bytes: page[53..56]))
        {
            throw new StorageFormatException("Reserved internal-header bytes must be zero.");
        }

        var parent = ReadOptionalPage(source: page[InternalIndexPageFormat.ParentOffset..]);
        var separatorCount = BinaryPrimitives.ReadUInt16LittleEndian(source: page[InternalIndexPageFormat.SeparatorCountOffset..]);
        var childCount = BinaryPrimitives.ReadUInt16LittleEndian(source: page[InternalIndexPageFormat.ChildCountOffset..]);

        if (separatorCount == 0 || childCount != separatorCount + 1)
        {
            throw new StorageCorruptionException("Internal child count must equal separator count plus one.");
        }

        var directoryEnd = BinaryPrimitives.ReadUInt32LittleEndian(source: page[InternalIndexPageFormat.SlotDirectoryEndOffset..]);
        var expectedDirectoryEnd = checked((uint)(InternalIndexPageFormat.HeaderLength + separatorCount * InternalIndexPageFormat.SlotLength));
        var keyStart = BinaryPrimitives.ReadUInt32LittleEndian(source: page[InternalIndexPageFormat.KeyDataStartOffset..]);

        if (directoryEnd != expectedDirectoryEnd || directoryEnd > keyStart || keyStart > page.Length)
        {
            throw new StorageCorruptionException("Internal slot and key regions overlap or exceed the page.");
        }

        List<PageId> children = [ReadChild(value: BinaryPrimitives.ReadUInt64LittleEndian(source: page[InternalIndexPageFormat.FirstChildOffset..]), pageId: expectedPageId)];
        List<sql_storage_engine.Models.Indexes.IndexKey> keys = [];
        var expectedKeyEnd = page.Length;

        for (var index = 0; index < separatorCount; index++)
        {
            var slot = page.Slice(start: InternalIndexPageFormat.HeaderLength + index * InternalIndexPageFormat.SlotLength, length: InternalIndexPageFormat.SlotLength);

            if (BinaryPrimitives.ReadUInt16LittleEndian(source: slot[6..]) != 0)
            {
                throw new StorageFormatException("Reserved internal-slot bytes must be zero.");
            }

            var offset = BinaryPrimitives.ReadUInt32LittleEndian(source: slot);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(source: slot[4..]);

            if (length == 0 || checked((ulong)offset + length) > (ulong)page.Length || offset < keyStart || checked((int)(offset + length)) != expectedKeyEnd)
            {
                throw new StorageCorruptionException("Internal key offset is malformed.");
            }

            sql_storage_engine.Models.Indexes.IndexKey key = indexKeys.CreateIndexKey(bytes: page.Slice(start: checked((int)offset), length: length));

            if (keys.Count > 0 && indexKeys.CompareIndexKeys(indexKeys: [keys[^1], key]) > 0)
            {
                throw new StorageCorruptionException("Internal separators are not ordered.");
            }

            keys.Add(item: key);
            expectedKeyEnd = checked((int)offset);
            children.Add(item: ReadChild(value: BinaryPrimitives.ReadUInt64LittleEndian(source: slot[8..]), pageId: expectedPageId));
        }

        if (expectedKeyEnd != keyStart || children.Distinct()
            .Count() != children.Count)
        {
            throw new StorageCorruptionException("Internal key packing or child IDs are invalid.");
        }

        var model = new InternalIndexPage
        {
            PageId = expectedPageId,
            ParentPageId = parent,
            Separators = keys.AsReadOnly(),
            Children = children.AsReadOnly()
        };

        ValidateModel(model: model);
        return model;
    }

    private static PageId ReadChild(ulong value, PageId pageId)
    {
        var child = new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = value
        };

        if (child.Value == 0 || child == pageId)
        {
            throw new StorageCorruptionException("Invalid internal child page ID.");
        }

        return child;
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
        _ => throw new StorageFormatException("Invalid nullable internal parent encoding.")};

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
}