// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Text;
using System.Buffers;
using System.Buffers.Binary;
using sql_storage_engine.Models.Backup;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.BackupFiles;
internal sealed partial class BackupFileContextService
{
    private const int MaximumMetadataBytes = BackupMetadataFormat.MaximumBytes;
    private const int MaximumStringBytes = BackupMetadataFormat.MaximumStringBytes;
    private const int MaximumFiles = BackupMetadataFormat.MaximumFiles;
    private const int MetadataHeaderBytes = BackupMetadataFormat.HeaderBytes;
    private const int MetadataChecksumBytes = BackupMetadataFormat.ChecksumBytes;
    private const ulong MetadataMagic = BackupMetadataFormat.Magic;

    private byte[] EncodeBackupManifest(BackupManifest backupManifest)
    {
        ArgumentNullException.ThrowIfNull(argument: backupManifest);
        ArgumentNullException.ThrowIfNull(argument: backupManifest.Files);

        if (backupManifest.PageSize <= 0 || backupManifest.Files.Count is 0 or > MaximumFiles)
        {
            throw new StorageFormatException(message: "Backup manifest page size or file count is invalid.");
        }

        var writer = new ArrayBufferWriter<byte>();
        WriteBackupBytes(writer: writer, bytes: backupManifest.DatabaseId.Value.ToByteArray(bigEndian: true));
        WriteBackupUInt16(writer: writer, value: backupManifest.FormatVersion);
        WriteBackupUInt32(writer: writer, value: checked((uint)backupManifest.PageSize));
        WriteBackupUInt64(writer: writer, value: backupManifest.StartLsn.Value);
        WriteBackupUInt64(writer: writer, value: backupManifest.EndLsn.Value);
        WriteBackupUInt64(writer: writer, value: checked((ulong)backupManifest.CreatedUtc.Ticks));
        WriteBackupUInt16(writer: writer, value: unchecked((ushort)(short)backupManifest.CreatedUtc.Offset.TotalMinutes));
        WriteBackupString(writer: writer, value: backupManifest.EngineVersion);
        WriteBackupUInt32(writer: writer, value: checked((uint)backupManifest.Files.Count));

        foreach (var file in backupManifest.Files)
        {
            ArgumentNullException.ThrowIfNull(argument: file);
            WriteBackupString(writer: writer, value: file.Name);
            WriteBackupString(writer: writer, value: file.Kind);

            if (file.Size < 0)
            {
                throw new StorageFormatException(message: "Backup file size cannot be negative.");
            }

            WriteBackupUInt64(writer: writer, value: checked((ulong)file.Size));
            WriteBackupChecksum(writer: writer, value: file.Sha256);
        }

        return EncodeBackupEnvelope(payload: writer.WrittenSpan, kind: 1);
    }

    private BackupManifest DecodeBackupManifest(byte[] bytes)
    {
        var end = ValidateBackupEnvelope(bytes: bytes, kind: 1);
        var offset = MetadataHeaderBytes;
        var databaseId = new DatabaseId { Value = new Guid(b: ReadBackupSpan(bytes: bytes, offset: ref offset, end: end, length: 16), bigEndian: true) };
        var formatVersion = ReadBackupUInt16(bytes: bytes, offset: ref offset, end: end);
        var pageSize = ReadBackupUInt32(bytes: bytes, offset: ref offset, end: end);

        if (pageSize == 0 || pageSize > int.MaxValue)
        {
            throw new StorageFormatException(message: "Invalid backup manifest page size.");
        }

        var startLsn = new LogSequenceNumber { Value = ReadBackupUInt64(bytes: bytes, offset: ref offset, end: end) };
        var endLsn = new LogSequenceNumber { Value = ReadBackupUInt64(bytes: bytes, offset: ref offset, end: end) };
        var createdUtc = ReadBackupTimestamp(bytes: bytes, offset: ref offset, end: end);
        var engineVersion = ReadBackupString(bytes: bytes, offset: ref offset, end: end);
        var count = ReadBackupUInt32(bytes: bytes, offset: ref offset, end: end);

        if (count == 0 || count > MaximumFiles || count > (end - offset) / 48)
        {
            throw new StorageFormatException(message: "Invalid backup manifest file count.");
        }

        var files = new BackupFile[checked((int)count)];

        for (var index = 0; index < files.Length; index++)
        {
            files[index] = new BackupFile { Name = ReadBackupString(bytes: bytes, offset: ref offset, end: end), Kind = ReadBackupString(bytes: bytes, offset: ref offset, end: end), Size = ReadBackupFileSize(bytes: bytes, offset: ref offset, end: end), Sha256 = Convert.ToHexString(bytes: ReadBackupSpan(bytes: bytes, offset: ref offset, end: end, length: 32)) };
        }

        RequireBackupEnd(offset: offset, end: end);

        return new BackupManifest { DatabaseId = databaseId, FormatVersion = formatVersion, PageSize = checked((int)pageSize), StartLsn = startLsn, EndLsn = endLsn, CreatedUtc = createdUtc, EngineVersion = engineVersion, Files = Array.AsReadOnly(array: files) };
    }

    private byte[] EncodeBackupTimeline(BackupTimeline backupTimeline)
    {
        ArgumentNullException.ThrowIfNull(argument: backupTimeline);
        var writer = new ArrayBufferWriter<byte>();
        WriteBackupBytes(writer: writer, bytes: backupTimeline.DatabaseId.ToByteArray(bigEndian: true));
        WriteBackupUInt64(writer: writer, value: backupTimeline.Timeline);
        WriteBackupUInt64(writer: writer, value: backupTimeline.ParentTimeline);
        WriteBackupUInt64(writer: writer, value: backupTimeline.ForkLsn);
        return EncodeBackupEnvelope(payload: writer.WrittenSpan, kind: 2);
    }

    private BackupTimeline DecodeBackupTimeline(byte[] bytes)
    {
        var end = ValidateBackupEnvelope(bytes: bytes, kind: 2);
        var offset = MetadataHeaderBytes;
        var timeline = new BackupTimeline { DatabaseId = new Guid(b: ReadBackupSpan(bytes: bytes, offset: ref offset, end: end, length: 16), bigEndian: true), Timeline = ReadBackupUInt64(bytes: bytes, offset: ref offset, end: end), ParentTimeline = ReadBackupUInt64(bytes: bytes, offset: ref offset, end: end), ForkLsn = ReadBackupUInt64(bytes: bytes, offset: ref offset, end: end) };
        RequireBackupEnd(offset: offset, end: end);
        return timeline;
    }

    private byte[] EncodeBackupEnvelope(ReadOnlySpan<byte> payload, ushort kind)
    {
        var bytes = new byte[MetadataHeaderBytes + payload.Length + MetadataChecksumBytes];
        BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes, value: MetadataMagic);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 8), value: BackupMetadataFormat.Version);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 10), value: kind);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes.AsSpan(start: 12), value: checked((uint)payload.Length));
        payload.CopyTo(destination: bytes.AsSpan(start: MetadataHeaderBytes));

        var checksum = broker.HashBytes(bytes: bytes.AsSpan(start: 0, length: bytes.Length - MetadataChecksumBytes)
            .ToArray());

        checksum.CopyTo(array: bytes, index: bytes.Length - MetadataChecksumBytes);
        return bytes;
    }

    private int ValidateBackupEnvelope(byte[] bytes, ushort kind)
    {
        ArgumentNullException.ThrowIfNull(argument: bytes);

        if (bytes.Length < MetadataHeaderBytes + MetadataChecksumBytes || bytes.Length > MaximumMetadataBytes)
        {
            throw new StorageFormatException(message: "Invalid backup metadata length; legacy JSON backups are unsupported.");
        }

        if (BinaryPrimitives.ReadUInt64LittleEndian(source: bytes) != MetadataMagic)
        {
            throw new StorageFormatException(message: "Invalid backup metadata marker; legacy JSON backups are unsupported.");
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(source: bytes.AsSpan(start: 8)) != BackupMetadataFormat.Version || BinaryPrimitives.ReadUInt16LittleEndian(source: bytes.AsSpan(start: 10)) != kind)
        {
            throw new StorageFormatException(message: "Unsupported backup metadata version or record type.");
        }

        var end = bytes.Length - MetadataChecksumBytes;

        if (BinaryPrimitives.ReadUInt32LittleEndian(source: bytes.AsSpan(start: 12)) != end - MetadataHeaderBytes)
        {
            throw new StorageFormatException(message: "Backup metadata payload length does not match its file.");
        }

        var checksum = broker.HashBytes(bytes: bytes.AsSpan(start: 0, length: end)
            .ToArray());

        if (!bytes.AsSpan(start: end)
            .SequenceEqual(other: checksum))
        {
            throw new StorageCorruptionException(message: "Backup metadata checksum does not match.");
        }

        return end;
    }

    private void WriteBackupString(ArrayBufferWriter<byte> writer, string value)
    {
        ArgumentNullException.ThrowIfNull(argument: value);

        try
        {
            var length = broker.CountUtf8Bytes(value: value);

            if (length > MaximumStringBytes)
            {
                throw new StorageFormatException(message: "Backup metadata string exceeds the binary format limit.");
            }

            RequireBackupCapacity(writer: writer, length: checked(length + 4));
            WriteBackupUInt32(writer: writer, value: checked((uint)length));
            WriteBackupBytes(writer: writer, bytes: broker.EncodeUtf8(value: value));
        }
        catch (EncoderFallbackException exception)
        {
            throw new StorageFormatException(message: "Backup metadata string is not valid UTF-8.", innerException: exception);
        }
    }

    private string ReadBackupString(byte[] bytes, ref int offset, int end)
    {
        var length = ReadBackupUInt32(bytes: bytes, offset: ref offset, end: end);

        if (length > MaximumStringBytes)
        {
            throw new StorageFormatException(message: "Invalid backup metadata string length.");
        }

        var value = ReadBackupSpan(bytes: bytes, offset: ref offset, end: end, length: checked((int)length));

        try
        {
            return broker.DecodeUtf8(bytes: value.ToArray());
        }
        catch (DecoderFallbackException exception)
        {
            throw new StorageFormatException(message: "Backup metadata string is not valid UTF-8.", innerException: exception);
        }
    }

    private static void WriteBackupChecksum(ArrayBufferWriter<byte> writer, string value)
    {
        if (value is null || value.Length != 64)
        {
            throw new StorageFormatException(message: "Backup file checksum must contain 32 hexadecimal bytes.");
        }

        try
        {
            WriteBackupBytes(writer: writer, bytes: Convert.FromHexString(s: value));
        }
        catch (FormatException exception)
        {
            throw new StorageFormatException(message: "Backup file checksum is not hexadecimal.", innerException: exception);
        }
    }

    private static long ReadBackupFileSize(byte[] bytes, ref int offset, int end)
    {
        var size = ReadBackupUInt64(bytes: bytes, offset: ref offset, end: end);

        if (size > long.MaxValue)
        {
            throw new StorageFormatException(message: "Invalid backup file size.");
        }

        return checked((long)size);
    }

    private static DateTimeOffset ReadBackupTimestamp(byte[] bytes, ref int offset, int end)
    {
        var ticks = unchecked((long)ReadBackupUInt64(bytes: bytes, offset: ref offset, end: end));
        var minutes = unchecked((short)ReadBackupUInt16(bytes: bytes, offset: ref offset, end: end));

        try
        {
            return new DateTimeOffset(ticks: ticks, offset: TimeSpan.FromMinutes(value: minutes));
        }
        catch (ArgumentException exception)
        {
            throw new StorageFormatException(message: "Invalid backup metadata timestamp.", innerException: exception);
        }
    }

    private static void WriteBackupUInt16(ArrayBufferWriter<byte> writer, ushort value)
    {
        RequireBackupCapacity(writer: writer, length: 2);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: writer.GetSpan(sizeHint: 2), value: value);
        writer.Advance(count: 2);
    }

    private static void WriteBackupUInt32(ArrayBufferWriter<byte> writer, uint value)
    {
        RequireBackupCapacity(writer: writer, length: 4);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: writer.GetSpan(sizeHint: 4), value: value);
        writer.Advance(count: 4);
    }

    private static void WriteBackupUInt64(ArrayBufferWriter<byte> writer, ulong value)
    {
        RequireBackupCapacity(writer: writer, length: 8);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: writer.GetSpan(sizeHint: 8), value: value);
        writer.Advance(count: 8);
    }

    private static void WriteBackupBytes(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        RequireBackupCapacity(writer: writer, length: bytes.Length);
        bytes.CopyTo(destination: writer.GetSpan(sizeHint: bytes.Length));
        writer.Advance(count: bytes.Length);
    }

    private static void RequireBackupCapacity(ArrayBufferWriter<byte> writer, int length)
    {
        if (length > MaximumMetadataBytes - MetadataHeaderBytes - MetadataChecksumBytes - writer.WrittenCount)
        {
            throw new StorageFormatException(message: "Backup metadata exceeds the binary format limit.");
        }
    }

    private static ushort ReadBackupUInt16(byte[] bytes, ref int offset, int end) =>
        BinaryPrimitives.ReadUInt16LittleEndian(source: ReadBackupSpan(bytes: bytes, offset: ref offset, end: end, length: 2));

    private static uint ReadBackupUInt32(byte[] bytes, ref int offset, int end) =>
        BinaryPrimitives.ReadUInt32LittleEndian(source: ReadBackupSpan(bytes: bytes, offset: ref offset, end: end, length: 4));

    private static ulong ReadBackupUInt64(byte[] bytes, ref int offset, int end) =>
        BinaryPrimitives.ReadUInt64LittleEndian(source: ReadBackupSpan(bytes: bytes, offset: ref offset, end: end, length: 8));

    private static ReadOnlySpan<byte> ReadBackupSpan(byte[] bytes, ref int offset, int end, int length)
    {
        if (length > end - offset)
        {
            throw new StorageFormatException(message: "Backup metadata payload is truncated.");
        }

        var value = bytes.AsSpan(start: offset, length: length);
        offset += length;
        return value;
    }

    private static void RequireBackupEnd(int offset, int end)
    {
        if (offset != end)
        {
            throw new StorageFormatException(message: "Backup metadata contains trailing payload bytes.");
        }
    }
}