// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Dependencies.JsonPayloads;

namespace sql_storage_engine.Brokers.JsonPayloads;
internal sealed class JsonPayloadBroker : IJsonPayloadBroker
{
    private readonly JsonPayloadDependency dependency = new();
    public void OpenDocument(string json) =>
        dependency.OpenDocument(json: json);

    public byte ReadKind(int node) =>
        (byte)dependency.ReadKind(node: node);

    public string ReadNumber(int node) =>
        dependency.ReadNumber(node: node);

    public string ReadString(int node) =>
        dependency.ReadString(node: node);

    public int[] ReadArray(int node) =>
        dependency.ReadArray(node: node);

    public (string[] Names, int[] Children) ReadObject(int node) =>
        dependency.ReadObject(node: node);

    public byte[] EncodeUtf8(string value) =>
        dependency.EncodeUtf8(value: value);

    public string DecodeUtf8(ReadOnlySpan<byte> bytes) =>
        dependency.DecodeUtf8(bytes: bytes);

    public void OpenWriter() =>
        dependency.OpenWriter();

    public void WriteNull() =>
        dependency.WriteNull();

    public void WriteBoolean(bool value) =>
        dependency.WriteBoolean(value: value);

    public void WriteNumber(string value) =>
        dependency.WriteNumber(value: value);

    public void WriteString(string value) =>
        dependency.WriteString(value: value);

    public void WriteProperty(string name) =>
        dependency.WriteProperty(name: name);

    public void StartArray() =>
        dependency.StartArray();

    public void EndArray() =>
        dependency.EndArray();

    public void StartObject() =>
        dependency.StartObject();

    public void EndObject() =>
        dependency.EndObject();

    public string FinishWriter() =>
        dependency.FinishWriter();

    public void Close() =>
        dependency.Close();
}