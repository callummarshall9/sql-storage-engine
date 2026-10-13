// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Schema;
using System.Collections.Generic;

namespace sql_storage_engine.Dependencies.SqlTypeDocuments;
internal sealed class SqlTypeDocumentDependency : IDisposable
{
    private StringReader? text;
    private XmlReader? reader;
    private JsonDocument? document;
    public void CheckSchemas(string[] definitions)
    {
        try
        {
            Compile(definitions: definitions);
        }
        catch (Exception exception)when (exception is XmlException or XmlSchemaException)
        {
            throw new InvalidDataException(message: exception.Message, innerException: exception);
        }
    }

    private static XmlSchemaSet Compile(string[] definitions)
    {
        var schemas = new XmlSchemaSet();

        foreach (var definition in definitions)
        {
            using var input = new StringReader(definition);
            using var schemaReader = XmlReader.Create(input: input, settings: new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, ConformanceLevel = ConformanceLevel.Document });
            schemas.Add(targetNamespace: null, schemaDocument: schemaReader);
        }

        schemas.Compile();
        return schemas;
    }

    public void CheckXml(string value, bool isDocument, string[]? definitions)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                ConformanceLevel = isDocument ? ConformanceLevel.Document : ConformanceLevel.Fragment
            };

            if (definitions != null)
            {
                settings.Schemas = Compile(definitions: definitions);
                settings.ValidationType = ValidationType.Schema;
                settings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings;
            }

            text = new StringReader(value);
            reader = XmlReader.Create(input: text, settings: settings);

            while (reader.Read())
            {
            }
        }
        catch (Exception exception)when (exception is XmlException or XmlSchemaException)
        {
            throw new InvalidDataException(message: exception.Message, innerException: exception);
        }
    }

    public int[] ReadJsonObservations(string value, int maximumDepth, int maximumUniqueKeys, int maximumCollectionItems, int maximumKeyBytes, int maximumStringBytes)
    {
        try
        {
            document = JsonDocument.Parse(json: value, options: new JsonDocumentOptions { MaxDepth = maximumDepth });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(message: exception.Message, innerException: exception);
        }

        var observations = new int[6];
        observations[0] = document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? 1 : 0;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<JsonElement>();
        pending.Push(item: document.RootElement);

        while (pending.Count != 0)
        {
            var element = pending.Pop();

            if (element.ValueKind == JsonValueKind.Object)
            {
                var count = 0;

                foreach (var property in element.EnumerateObject())
                {
                    count++;
                    observations[1] = Math.Max(val1: observations[1], val2: count);

                    if (count > maximumCollectionItems)
                    {
                        return observations;
                    }

                    var keyBytes = Encoding.UTF8.GetByteCount(s: property.Name);
                    observations[4] = Math.Max(val1: observations[4], val2: keyBytes);

                    if (keyBytes > maximumKeyBytes)
                    {
                        return observations;
                    }

                    keys.Add(item: property.Name);
                    observations[3] = keys.Count;

                    if (keys.Count > maximumUniqueKeys)
                    {
                        return observations;
                    }

                    pending.Push(item: property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var count = element.GetArrayLength();
                observations[2] = Math.Max(val1: observations[2], val2: count);

                if (count > maximumCollectionItems)
                {
                    return observations;
                }

                foreach (var item in element.EnumerateArray())
                {
                    pending.Push(item: item);
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var bytes = Encoding.UTF8.GetByteCount(s: element.GetString()!);
                observations[5] = Math.Max(val1: observations[5], val2: bytes);

                if (bytes > maximumStringBytes)
                {
                    return observations;
                }
            }
        }

        return observations;
    }

    public void Dispose()
    {
        reader?.Dispose();
        text?.Dispose();
        document?.Dispose();
    }

    public float[] ReadJsonVector(string value)
    {
        document = JsonDocument.Parse(json: value);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException();
        }

        var values = new List<float>();

        foreach (var item in document.RootElement.EnumerateArray())
        {
            values.Add(item: item.GetSingle());
        }

        return values.ToArray();
    }

    public string WriteJsonVector(float[] values)
    {
        return JsonSerializer.Serialize(value: values);
    }

    public byte[] ReadUtf8Bytes(string value)
    {
        return Encoding.UTF8.GetBytes(s: value);
    }
}