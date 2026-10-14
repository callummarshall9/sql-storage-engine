// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.UpgradeSerialization;
internal interface IUpgradeSerializationBroker
{
    byte[] Serialize<T>(T value);

    (T? Value, string? Error, Exception? Cause) TryDeserialize<T>(ReadOnlyMemory<byte> bytes);
}