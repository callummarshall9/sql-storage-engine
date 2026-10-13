// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Dependencies.SecuritySerialization;

namespace sql_storage_engine.Brokers.SecuritySerialization;
internal sealed class SecuritySerializationBroker : ISecuritySerializationBroker
{
    public byte[] Serialize<T>(T value)
    {
        using var serializer = new SecuritySerializationDependency();
        return serializer.Serialize(value: value);
    }

    public T? Deserialize<T>(ReadOnlyMemory<byte> bytes)
    {
        using var serializer = new SecuritySerializationDependency();
        return serializer.Deserialize<T>(bytes: bytes);
    }

    public (T? Value, string? Error, Exception? Cause) TryDeserialize<T>(ReadOnlyMemory<byte> bytes)
    {
        using var serializer = new SecuritySerializationDependency();
        return serializer.TryDeserialize<T>(bytes: bytes);
    }

    public bool HasProperty(ReadOnlyMemory<byte> bytes, string name)
    {
        using var serializer = new SecuritySerializationDependency();
        return serializer.HasProperty(bytes: bytes, name: name);
    }
}