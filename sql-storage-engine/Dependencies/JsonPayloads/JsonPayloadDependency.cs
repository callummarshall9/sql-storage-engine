// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace sql_storage_engine.Dependencies.JsonPayloads;
internal sealed class JsonPayloadDependency : IDisposable
{
    private readonly object sessionSync = new();
    private bool disposed;
    private JsonDocument? document;
    private readonly List<JsonElement> elements = new();
    private readonly MemoryStream output = new();
    private Utf8JsonWriter? writer;
    private readonly UTF8Encoding utf8 = new(false, true);
    public void OpenDocument(string json)
    {
        Monitor.Enter(obj: sessionSync);
        ObjectDisposedException.ThrowIf(condition: disposed, instance: this);

        try
        {
            document = JsonDocument.Parse(json: json, options: new JsonDocumentOptions { MaxDepth = 128 });
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(message: exception.Message, innerException: exception);
        }

        elements.Add(item: document.RootElement);
    }

    public JsonValueKind ReadKind(int node) =>
        elements[node].ValueKind;

    public string ReadNumber(int node) =>
        elements[node].GetRawText();

    public string ReadString(int node) =>
        elements[node].GetString()!;

    public int[] ReadArray(int node)
    {
        var children = new List<int>();

        foreach (var element in elements[node].EnumerateArray())
        {
            children.Add(item: elements.Count);
            elements.Add(item: element);
        }

        return children.ToArray();
    }

    public (string[] Names, int[] Children) ReadObject(int node)
    {
        var names = new List<string>();
        var children = new List<int>();

        foreach (var property in elements[node].EnumerateObject())
        {
            names.Add(item: property.Name);
            children.Add(item: elements.Count);
            elements.Add(item: property.Value);
        }

        return (names.ToArray(), children.ToArray());
    }

    public byte[] EncodeUtf8(string value) =>
        utf8.GetBytes(s: value);

    public string DecodeUtf8(ReadOnlySpan<byte> bytes) =>
        utf8.GetString(bytes: bytes);

    public void OpenWriter()
    {
        Monitor.Enter(obj: sessionSync);
        ObjectDisposedException.ThrowIf(condition: disposed, instance: this);
        writer = new Utf8JsonWriter(output);
    }

    public void WriteNull() =>
        writer!.WriteNullValue();

    public void WriteBoolean(bool value) =>
        writer!.WriteBooleanValue(value: value);

    public void WriteNumber(string value)
    {
        try
        {
            writer!.WriteRawValue(json: value, skipInputValidation: false);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(message: exception.Message, innerException: exception);
        }
    }

    public void WriteString(string value) =>
        writer!.WriteStringValue(value: value);

    public void WriteProperty(string name) =>
        writer!.WritePropertyName(propertyName: name);

    public void StartArray() =>
        writer!.WriteStartArray();

    public void EndArray() =>
        writer!.WriteEndArray();

    public void StartObject() =>
        writer!.WriteStartObject();

    public void EndObject() =>
        writer!.WriteEndObject();

    public string FinishWriter()
    {
        writer!.Flush();
        return utf8.GetString(bytes: output.ToArray());
    }

    public void Close()
    {
        if (!Monitor.IsEntered(obj: sessionSync))
        {
            return;
        }

        try
        {
            if (!disposed)
            {
                ResetSession();
            }
        }
        finally
        {
            Monitor.Exit(obj: sessionSync);
        }
    }

    public void Dispose()
    {
        lock (sessionSync)
        {
            if (disposed)
            {
                return;
            }

            ResetSession();
            output.Dispose();
            disposed = true;
        }
    }

    private void ResetSession()
    {
        writer?.Dispose();
        writer = null;
        document?.Dispose();
        document = null;
        elements.Clear();
        output.SetLength(value: 0);
        output.Position = 0;
    }
}