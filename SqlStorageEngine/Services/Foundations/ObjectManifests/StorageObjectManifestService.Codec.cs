// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers;
using System.Buffers.Binary;
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.ObjectManifests;
internal sealed partial class StorageObjectManifestService
{
    private byte[] EncodeStorageObjectManifest(StorageObjectManifest storageObjectManifest)
    {
        var writer = new ArrayBufferWriter<byte>();
        AppendBytes(writer: writer, bytes: storageObjectManifest.DatabaseId.ToByteArray(bigEndian: true));
        AppendInt64(writer: writer, value: storageObjectManifest.Commit);
        AppendInt32(writer: writer, value: storageObjectManifest.PageSize);
        AppendInt32(writer: writer, value: storageObjectManifest.SegmentSize);
        AppendInt64(writer: writer, value: storageObjectManifest.FileLength);
        AppendInt32(writer: writer, value: storageObjectManifest.Segments!.Length);

        foreach (var segment in storageObjectManifest.Segments)
        {
            var key = broker.EncodeUtf8(text: segment.Key!);

            if (key.Length > MaximumKeyBytes)
            {
                throw new StorageFormatException(message: "Object storage manifest key exceeds its UTF-8 limit.");
            }

            AppendInt32(writer: writer, value: segment.Index);
            AppendInt32(writer: writer, value: segment.Length);
            AppendInt32(writer: writer, value: key.Length);
            AppendBytes(writer: writer, bytes: key);
            AppendBytes(writer: writer, bytes: segment.Checksum!);
        }

        var bytes = new byte[16 + writer.WrittenCount + 32];
        "SSEOBJ01"u8.CopyTo(destination: bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 8), value: 1);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 10), value: 1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes.AsSpan(start: 12), value: checked((uint)writer.WrittenCount));
        writer.WrittenSpan.CopyTo(destination: bytes.AsSpan(start: 16));

        broker.Hash(bytes: bytes.AsSpan(start: 0, length: bytes.Length - 32))
            .CopyTo(array: bytes, index: bytes.Length - 32);

        return bytes;
    }

    private StorageObjectManifest DecodeStorageObjectManifest(byte[] bytes)
    {
        if (bytes.Length is < 92 or > MaximumBytes || !bytes.AsSpan(start: 0, length: 8)
            .SequenceEqual(other: "SSEOBJ01"u8) || BinaryPrimitives.ReadUInt16LittleEndian(source: bytes.AsSpan(start: 8)) != 1 || BinaryPrimitives.ReadUInt16LittleEndian(source: bytes.AsSpan(start: 10)) != 1 || BinaryPrimitives.ReadUInt32LittleEndian(source: bytes.AsSpan(start: 12)) != bytes.Length - 48)
        {
            throw new StorageFormatException(message: "Object storage manifest envelope is invalid.");
        }

        if (!bytes.AsSpan(start: bytes.Length - 32)
            .SequenceEqual(other: broker.Hash(bytes: bytes.AsSpan(start: 0, length: bytes.Length - 32))))
        {
            throw new StorageFormatException(message: "Object storage manifest checksum is invalid.");
        }

        var offset = 16;
        var end = bytes.Length - 32;
        var databaseId = new Guid(b: TakeBytes(bytes: bytes, offset: ref offset, end: end, count: 16), bigEndian: true);
        var commit = TakeInt64(bytes: bytes, offset: ref offset, end: end);
        var pageSize = TakeInt32(bytes: bytes, offset: ref offset, end: end);
        var segmentSize = TakeInt32(bytes: bytes, offset: ref offset, end: end);
        var fileLength = TakeInt64(bytes: bytes, offset: ref offset, end: end);
        var count = TakeInt32(bytes: bytes, offset: ref offset, end: end);

        if (count is <= 0 or > MaximumSegments || count > (end - offset) / 44)
        {
            throw new StorageFormatException(message: "Object storage manifest segment count exceeds the payload.");
        }

        var segments = new StorageObjectSegment[count];

        for (var index = 0; index < count; index++)
        {
            var segmentIndex = TakeInt32(bytes: bytes, offset: ref offset, end: end);
            var length = TakeInt32(bytes: bytes, offset: ref offset, end: end);
            var keyLength = TakeInt32(bytes: bytes, offset: ref offset, end: end);

            if (keyLength is <= 0 or > MaximumKeyBytes)
            {
                throw new StorageFormatException(message: "Object storage manifest key length is invalid.");
            }

            var key = broker.DecodeUtf8(bytes: TakeBytes(bytes: bytes, offset: ref offset, end: end, count: keyLength));

            var checksum = TakeBytes(bytes: bytes, offset: ref offset, end: end, count: 32)
                .ToArray();

            segments[index] = new StorageObjectSegment { Index = segmentIndex, Length = length, Key = key, Checksum = checksum };
        }

        if (offset != end)
        {
            throw new StorageFormatException(message: "Object storage manifest has trailing payload bytes.");
        }

        return new StorageObjectManifest { DatabaseId = databaseId, Commit = commit, PageSize = pageSize, SegmentSize = segmentSize, FileLength = fileLength, Segments = segments };
    }

    private static void AppendBytes(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumBytes - 48 - writer.WrittenCount)
        {
            throw new StorageFormatException(message: "Object storage manifest exceeds its size limit.");
        }

        bytes.CopyTo(destination: writer.GetSpan(sizeHint: bytes.Length));
        writer.Advance(count: bytes.Length);
    }

    private static void AppendInt32(ArrayBufferWriter<byte> writer, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(destination: bytes, value: value);
        AppendBytes(writer: writer, bytes: bytes);
    }

    private static void AppendInt64(ArrayBufferWriter<byte> writer, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(destination: bytes, value: value);
        AppendBytes(writer: writer, bytes: bytes);
    }

    private static ReadOnlySpan<byte> TakeBytes(byte[] bytes, ref int offset, int end, int count)
    {
        if (count < 0 || count > end - offset)
        {
            throw new StorageFormatException(message: "Object storage manifest payload is truncated.");
        }

        var result = bytes.AsSpan(start: offset, length: count);
        offset += count;
        return result;
    }

    private static int TakeInt32(byte[] bytes, ref int offset, int end) =>
        BinaryPrimitives.ReadInt32LittleEndian(source: TakeBytes(bytes: bytes, offset: ref offset, end: end, count: 4));

    private static long TakeInt64(byte[] bytes, ref int offset, int end) =>
        BinaryPrimitives.ReadInt64LittleEndian(source: TakeBytes(bytes: bytes, offset: ref offset, end: end, count: 8));
}