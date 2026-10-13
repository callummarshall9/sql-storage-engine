// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.XmlPayloads;
internal interface IXmlPayloadService
{
    byte[] EncodeXmlPayload(string xml);

    string DecodeXmlPayload(ReadOnlySpan<byte> source);

    ColumnSetXmlElement[] ReadColumnSetXmlElements(string xml);

    string WriteColumnSetXmlElements(ColumnSetXmlElement[] columnSetXmlElements);
}