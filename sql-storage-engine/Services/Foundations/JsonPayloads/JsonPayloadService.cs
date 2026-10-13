// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Exceptions;
using System.Collections.Generic;
using System.Buffers.Binary;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Brokers.JsonPayloads;

namespace sql_storage_engine.Services.Foundations.JsonPayloads;
internal sealed partial class JsonPayloadService : IJsonPayloadService
{
    private readonly JsonPayloadBroker broker = new();
    private enum Tag : byte
    {
        Null,
        False,
        True,
        Number,
        String,
        Array,
        Object
    }

    public byte[] EncodeJsonPayload(string json) =>
        TryCatch(operation: () =>
    {
        try
        {
            ValidateJsonPayload(json: json);
            broker.OpenDocument(json: json);

            var output = new List<byte>
            {
                1
            };

            WriteElement(output: output, node: 0);
            return output.ToArray();
        }
        finally
        {
            broker.Close();
        }
    });

    public string DecodeJsonPayload(ReadOnlySpan<byte> source) =>
        TryCatch(operation: context =>
    {
        try
        {
            ValidateJsonPayloadSource(source: context.source);

            var cursor = new JsonPayloadCursor
            {
                Source = context.source[1..]
            };

            broker.OpenWriter();
            WriteValue(cursor: ref cursor, depth: 0);

            if (cursor.Offset != cursor.Source.Length)
            {
                throw new StorageFormatException(message: "Native JSON contains trailing bytes.");
            }

            return broker.FinishWriter();
        }
        finally
        {
            broker.Close();
        }
    }, context: new ReadableJsonPayloadContext { source = source });

    private void WriteElement(List<byte> output, int node)
    {
        switch (broker.ReadKind(node: node))
        {
            case 7:
                output.Add(item: (byte)Tag.Null);
                break;
            case 6:
                output.Add(item: (byte)Tag.False);
                break;
            case 5:
                output.Add(item: (byte)Tag.True);
                break;
            case 4:
                output.Add(item: (byte)Tag.Number);
                WriteString(output: output, value: broker.ReadNumber(node: node));
                break;
            case 3:
                output.Add(item: (byte)Tag.String);
                WriteString(output: output, value: broker.ReadString(node: node));
                break;
            case 2:
                output.Add(item: (byte)Tag.Array);
                var children = broker.ReadArray(node: node);
                WriteInt32(output: output, value: children.Length);
                foreach (var child in children)
                {
                    WriteElement(output: output, node: child);
                }

                break;
            case 1:
                output.Add(item: (byte)Tag.Object);
                var properties = broker.ReadObject(node: node);
                WriteInt32(output: output, value: properties.Names.Length);
                for (var index = 0; index < properties.Names.Length; index++)
                {
                    WriteString(output: output, value: properties.Names[index]);
                    WriteElement(output: output, node: properties.Children[index]);
                }

                break;
            default:
                throw new JsonPayloadFormatException(message: "Unsupported JSON token.");
        }
    }

    private static void WriteInt32(List<byte> output, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(destination: bytes, value: value);

        foreach (var item in bytes)
        {
            output.Add(item: item);
        }
    }

    private void WriteString(List<byte> output, string value)
    {
        var bytes = broker.EncodeUtf8(value: value);
        WriteInt32(output: output, value: bytes.Length);
        output.AddRange(collection: bytes);
    }

    private void WriteValue(ref JsonPayloadCursor cursor, int depth)
    {
        if (depth > 128)
        {
            throw new StorageFormatException(message: "Native JSON exceeds the supported nesting depth.");
        }

        switch ((Tag)ReadByte(cursor: ref cursor))
        {
            case Tag.Null:
                broker.WriteNull();
                break;
            case Tag.False:
                broker.WriteBoolean(value: false);
                break;
            case Tag.True:
                broker.WriteBoolean(value: true);
                break;
            case Tag.Number:
                broker.WriteNumber(value: ReadString(cursor: ref cursor));
                break;
            case Tag.String:
                broker.WriteString(value: ReadString(cursor: ref cursor));
                break;
            case Tag.Array:
                broker.StartArray();
                for (var count = ReadCount(cursor: ref cursor); count > 0; count--)
                {
                    WriteValue(cursor: ref cursor, depth: depth + 1);
                }

                broker.EndArray();
                break;
            case Tag.Object:
                broker.StartObject();
                for (var count = ReadCount(cursor: ref cursor); count > 0; count--)
                {
                    broker.WriteProperty(name: ReadString(cursor: ref cursor));
                    WriteValue(cursor: ref cursor, depth: depth + 1);
                }

                broker.EndObject();
                break;
            default:
                throw new StorageFormatException(message: "Native JSON contains an unknown token.");
        }
    }

    private static byte ReadByte(ref JsonPayloadCursor cursor)
    {
        Require(cursor: cursor, count: 1);
        return cursor.Source[cursor.Offset++];
    }

    private static int ReadCount(ref JsonPayloadCursor cursor)
    {
        Require(cursor: cursor, count: 4);
        var value = BinaryPrimitives.ReadInt32LittleEndian(source: cursor.Source[cursor.Offset..]);
        cursor.Offset += 4;

        if (value < 0)
        {
            throw new StorageFormatException(message: "Native JSON count is negative.");
        }

        return value;
    }

    private string ReadString(ref JsonPayloadCursor cursor)
    {
        var length = ReadCount(cursor: ref cursor);
        Require(cursor: cursor, count: length);
        var value = broker.DecodeUtf8(bytes: cursor.Source.Slice(start: cursor.Offset, length: length));
        cursor.Offset += length;
        return value;
    }

    private static void Require(JsonPayloadCursor cursor, int count)
    {
        if (cursor.Offset > cursor.Source.Length - count)
        {
            throw new StorageFormatException(message: "Native JSON is truncated.");
        }
    }
}