// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.XmlPayloads;
internal sealed partial class XmlPayloadService
{
    private static void ValidateXmlPayload(string xml) =>
        ArgumentNullException.ThrowIfNull(argument: xml);

    private static void ValidateXmlPayloadSource(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty || source[0] != 1)
        {
            throw new StorageFormatException(message: "Unsupported native XML format version.");
        }
    }

    private static void ValidateColumnSetXmlElements(ColumnSetXmlElement[] columnSetXmlElements)
    {
        ArgumentNullException.ThrowIfNull(argument: columnSetXmlElements);

        foreach (var element in columnSetXmlElements)
        {
            ArgumentNullException.ThrowIfNull(argument: element);
            ArgumentException.ThrowIfNullOrWhiteSpace(argument: element.Name);
            ArgumentNullException.ThrowIfNull(argument: element.Value);
        }
    }
}