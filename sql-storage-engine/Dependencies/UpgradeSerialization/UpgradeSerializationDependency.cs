// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Text.Json;

namespace sql_storage_engine.Dependencies.UpgradeSerialization;
internal sealed class UpgradeSerializationDependency : IDisposable
{
    private JsonDocument? document;
    public byte[] Serialize<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value: value);

    public (T? Value, string? Error, Exception? Cause) TryDeserialize<T>(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            document = JsonDocument.Parse(utf8Json: bytes);
            return (document.RootElement.Deserialize<T>(), null, null);
        }
        catch (JsonException exception)
        {
            return (default, exception.Message, exception);
        }
    }

    public void Dispose() =>
        document?.Dispose();
}