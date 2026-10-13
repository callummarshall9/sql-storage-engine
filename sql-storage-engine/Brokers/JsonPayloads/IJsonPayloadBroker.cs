// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.JsonPayloads;
internal interface IJsonPayloadBroker
{
    void OpenDocument(string json);

    byte ReadKind(int node);

    string ReadNumber(int node);

    string ReadString(int node);

    int[] ReadArray(int node);

    (string[] Names, int[] Children) ReadObject(int node);

    byte[] EncodeUtf8(string value);

    string DecodeUtf8(ReadOnlySpan<byte> bytes);

    void OpenWriter();

    void WriteNull();

    void WriteBoolean(bool value);

    void WriteNumber(string value);

    void WriteString(string value);

    void WriteProperty(string name);

    void StartArray();

    void EndArray();

    void StartObject();

    void EndObject();

    string FinishWriter();

    void Close();
}