// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.SecurityEvents;
internal interface ISecurityEventFileBroker
{
    bool Exists(string path);

    long ReadLength(string path);

    byte[] ReadBytes(string path);

    void Flush(string path);

    void Write(string path, ReadOnlyMemory<byte> bytes);

    void Publish(string source, string destination);

    void Delete(string path);

    byte[] Serialize<T>(T value);

    T? Deserialize<T>(ReadOnlyMemory<byte> bytes);

    byte[] Hash(ReadOnlyMemory<byte> bytes);

    bool Compare(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right);
}