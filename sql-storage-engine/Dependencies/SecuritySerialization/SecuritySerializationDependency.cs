// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Text.Json;

namespace sql_storage_engine.Dependencies.SecuritySerialization;
internal sealed class SecuritySerializationDependency : IDisposable
{
    private JsonDocument? document;
    public byte[] Serialize<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value: value);

    public T? Deserialize<T>(ReadOnlyMemory<byte> bytes) =>
        JsonSerializer.Deserialize<T>(utf8Json: bytes.Span);

    public (T? Value, string? Error, Exception? Cause) TryDeserialize<T>(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            return (JsonSerializer.Deserialize<T>(utf8Json: bytes.Span), null, null);
        }
        catch (JsonException exception)
        {
            return (default, exception.Message, exception);
        }
    }

    public bool HasProperty(ReadOnlyMemory<byte> bytes, string name)
    {
        document?.Dispose();
        document = JsonDocument.Parse(utf8Json: bytes);
        return document.RootElement.TryGetProperty(propertyName: name, value: out _);
    }

    public void Dispose() =>
        document?.Dispose();
}