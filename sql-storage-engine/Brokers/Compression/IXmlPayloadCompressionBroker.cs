// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.Compression;
internal interface IXmlPayloadCompressionBroker
{
    byte[] Compress(string xml);

    string Decompress(ReadOnlySpan<byte> source);

    (string NamespaceName, string Name, string Value, string? Nil)[] ReadElements(string xml);

    string WriteElements((string Name, string Value)[] elements);
}