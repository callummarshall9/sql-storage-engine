// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.SecuritySerialization;
internal interface ISecuritySerializationBroker
{
    byte[] Serialize<T>(T value);

    T? Deserialize<T>(ReadOnlyMemory<byte> bytes);

    bool HasProperty(ReadOnlyMemory<byte> bytes, string name);

    (T? Value, string? Error, Exception? Cause) TryDeserialize<T>(ReadOnlyMemory<byte> bytes);
}