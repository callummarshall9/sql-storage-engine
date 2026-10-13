// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Dependencies.SecurityEvents;

namespace sql_storage_engine.Brokers.SecurityEvents;
internal sealed class SecurityEventFileBroker : ISecurityEventFileBroker
{
    public bool Exists(string path)
    {
        using var file = new SecurityEventFileDependency();
        return file.Exists(path: path);
    }

    public long ReadLength(string path)
    {
        using var file = new SecurityEventFileDependency();
        return file.ReadLength(path: path);
    }

    public byte[] ReadBytes(string path)
    {
        using var file = new SecurityEventFileDependency();
        return file.ReadBytes(path: path);
    }

    public void Flush(string path)
    {
        using var file = new SecurityEventFileDependency();
        file.Flush(path: path);
    }

    public void Write(string path, ReadOnlyMemory<byte> bytes)
    {
        using var file = new SecurityEventFileDependency();
        file.Write(path: path, bytes: bytes);
    }

    public void Publish(string source, string destination)
    {
        using var file = new SecurityEventFileDependency();
        file.Publish(source: source, destination: destination);
    }

    public void Delete(string path)
    {
        using var file = new SecurityEventFileDependency();
        file.Delete(path: path);
    }

    public byte[] Serialize<T>(T value)
    {
        using var file = new SecurityEventFileDependency();
        return file.Serialize(value: value);
    }

    public T? Deserialize<T>(ReadOnlyMemory<byte> bytes)
    {
        using var file = new SecurityEventFileDependency();
        return file.Deserialize<T>(bytes: bytes);
    }

    public byte[] Hash(ReadOnlyMemory<byte> bytes)
    {
        using var file = new SecurityEventFileDependency();
        return file.Hash(bytes: bytes);
    }

    public bool Compare(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right)
    {
        using var file = new SecurityEventFileDependency();
        return file.Compare(left: left, right: right);
    }
}