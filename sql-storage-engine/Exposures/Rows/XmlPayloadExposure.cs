// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Services.Foundations.XmlPayloads;

namespace sql_storage_engine.Exposures.Rows;
public static class XmlPayloadExposure
{
    private static readonly IXmlPayloadService service = new XmlPayloadService();
    public static byte[] EncodeXmlPayload(string xml) =>
        service.EncodeXmlPayload(xml: xml);

    public static string DecodeXmlPayload(ReadOnlySpan<byte> source) =>
        service.DecodeXmlPayload(source: source);
}