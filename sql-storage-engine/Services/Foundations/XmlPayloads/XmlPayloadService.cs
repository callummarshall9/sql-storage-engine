// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Brokers.Compression;

namespace sql_storage_engine.Services.Foundations.XmlPayloads;
internal sealed partial class XmlPayloadService : IXmlPayloadService
{
    private static readonly IXmlPayloadCompressionBroker compression = new XmlPayloadCompressionBroker();
    public byte[] EncodeXmlPayload(string xml) =>
        TryCatch(operation: () =>
    {
        ValidateXmlPayload(xml: xml);
        var compressed = compression.Compress(xml: xml);
        var result = new byte[checked(compressed.Length + 1)];
        result[0] = 1;
        compressed.CopyTo(array: result, index: 1);
        return result;
    });

    public string DecodeXmlPayload(ReadOnlySpan<byte> source) =>
        TryCatch(operation: context =>
    {
        ValidateXmlPayloadSource(source: context.source);
        return compression.Decompress(source: context.source[1..]);
    }, context: new ReadableXmlPayloadContext { source = source });

    public ColumnSetXmlElement[] ReadColumnSetXmlElements(string xml) =>
        TryCatch(operation: () =>
    {
        ValidateXmlPayload(xml: xml);

        return compression.ReadElements(xml: xml)
            .Select(selector: element => new ColumnSetXmlElement { NamespaceName = element.NamespaceName, Name = element.Name, Value = element.Value, Nil = element.Nil })
            .ToArray();
    });

    public string WriteColumnSetXmlElements(ColumnSetXmlElement[] columnSetXmlElements) =>
        TryCatch(operation: () =>
    {
        ValidateColumnSetXmlElements(columnSetXmlElements: columnSetXmlElements);

        return compression.WriteElements(elements: columnSetXmlElements.Select(selector: element => (element.Name!, element.Value!))
            .ToArray());
    });
}