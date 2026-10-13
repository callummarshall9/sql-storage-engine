// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Dependencies.Compression;

namespace sql_storage_engine.Brokers.Compression;
internal sealed class XmlPayloadCompressionBroker : IXmlPayloadCompressionBroker
{
    public byte[] Compress(string xml)
    {
        using var dependency = new XmlPayloadCompressionDependency();
        return dependency.Compress(xml: xml);
    }

    public string Decompress(ReadOnlySpan<byte> source)
    {
        using var dependency = new XmlPayloadCompressionDependency();
        return dependency.Decompress(source: source);
    }

    public (string NamespaceName, string Name, string Value, string? Nil)[] ReadElements(string xml)
    {
        using var dependency = new XmlPayloadCompressionDependency();
        return dependency.ReadElements(xml: xml);
    }

    public string WriteElements((string Name, string Value)[] elements)
    {
        using var dependency = new XmlPayloadCompressionDependency();
        return dependency.WriteElements(elements: elements);
    }
}