// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Rows;
/// <summary>Host-provided behavior for one SQL CLR UDT. The storage engine never loads untrusted assemblies itself.</summary>
public interface ISqlClrTypeRuntime
{
    ReadOnlyMemory<byte> Parse(string text);

    string Format(ReadOnlyMemory<byte> serializedValue);

    bool Validate(ReadOnlyMemory<byte> serializedValue);

    int Compare(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right);
}