// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace sql_storage_engine.Dependencies.SecurityEvents;
internal sealed class SecurityEventFileDependency : IDisposable
{
    private FileStream? stream;
    public bool Exists(string path) =>
        File.Exists(path: path);

    public long ReadLength(string path) =>
        new FileInfo(fileName: path).Length;

    public byte[] ReadBytes(string path) =>
        File.ReadAllBytes(path: path);

    public void Flush(string path)
    {
        stream = new FileStream(path: path, mode: FileMode.Open, access: FileAccess.Write, share: FileShare.Read);
        stream.Flush(flushToDisk: true);
    }

    public void Write(string path, ReadOnlyMemory<byte> bytes)
    {
        stream = new FileStream(path: path, mode: FileMode.Create, access: FileAccess.Write, share: FileShare.None);
        stream.Write(buffer: bytes.Span);
        stream.Flush(flushToDisk: true);
    }

    public void Publish(string source, string destination) =>
        File.Move(sourceFileName: source, destFileName: destination, overwrite: true);

    public void Delete(string path) =>
        File.Delete(path: path);

    public byte[] Serialize<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value: value);

    public T? Deserialize<T>(ReadOnlyMemory<byte> bytes) =>
        JsonSerializer.Deserialize<T>(utf8Json: bytes.Span);

    public byte[] Hash(ReadOnlyMemory<byte> bytes) =>
        SHA256.HashData(source: bytes.Span);

    public bool Compare(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right) =>
        CryptographicOperations.FixedTimeEquals(left: left.Span, right: right.Span);

    public void Dispose() =>
        stream?.Dispose();
}