// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Dependencies.UpgradeSerialization;

namespace sql_storage_engine.Brokers.UpgradeSerialization;
internal sealed class UpgradeSerializationBroker : IUpgradeSerializationBroker
{
    public byte[] Serialize<T>(T value)
    {
        using var serialization = new UpgradeSerializationDependency();
        return serialization.Serialize(value: value);
    }

    public (T? Value, string? Error, Exception? Cause) TryDeserialize<T>(ReadOnlyMemory<byte> bytes)
    {
        using var serialization = new UpgradeSerializationDependency();
        return serialization.TryDeserialize<T>(bytes: bytes);
    }
}