// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace sql_storage_engine.Dependencies.Compression;
internal sealed class XmlPayloadCompressionDependency : IDisposable
{
    private readonly MemoryStream output = new();
    private readonly UTF8Encoding utf8 = new(false, true);
    public byte[] Compress(string xml)
    {
        using (var compressor = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = utf8.GetBytes(s: xml);
            compressor.Write(buffer: bytes);
        }

        return output.ToArray();
    }

    public string Decompress(ReadOnlySpan<byte> source)
    {
        using var input = new MemoryStream(source.ToArray(), writable: false);
        using var decompressor = new BrotliStream(input, CompressionMode.Decompress);
        decompressor.CopyTo(destination: output);
        return utf8.GetString(bytes: output.ToArray());
    }

    public void Dispose() =>
        output.Dispose();

    public (string NamespaceName, string Name, string Value, string? Nil)[] ReadElements(string xml) =>
        XDocument.Parse(text: "<columnSet>" + xml + "</columnSet>", options: LoadOptions.PreserveWhitespace)
            .Root!.Elements()
            .Select(selector: element => (element.Name.NamespaceName, XmlConvert.DecodeName(name: element.Name.LocalName), element.Value, element.Attribute(name: XNamespace.Get(namespaceName: "http://www.w3.org/2001/XMLSchema-instance") + "nil")?.Value))
            .ToArray();

    public string WriteElements((string Name, string Value)[] elements) =>
        string.Concat(values: elements.Select(selector: element => new XElement(name: XmlConvert.EncodeName(name: element.Name), content: element.Value).ToString(options: SaveOptions.DisableFormatting)));
}