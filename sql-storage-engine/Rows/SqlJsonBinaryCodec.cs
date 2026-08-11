using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Rows;

/// <summary>A deterministic typed binary representation for the native JSON storage family.</summary>
internal static class SqlJsonBinaryCodec
{
    private const byte Version = 1;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private enum Tag : byte { Null, False, True, Number, String, Array, Object }

    public static byte[] Encode(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 128 });
        var output = new ArrayBufferWriter<byte>();
        WriteByte(output, Version);
        WriteElement(output, document.RootElement);
        return output.WrittenSpan.ToArray();
    }

    public static string Decode(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty || source[0] != Version) throw new StorageFormatException("Unsupported native JSON format version.");
        var reader = new Reader(source[1..]);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) reader.WriteValue(writer);
        if (!reader.End) throw new StorageFormatException("Native JSON contains trailing bytes.");
        return Utf8.GetString(stream.ToArray());
    }

    private static void WriteElement(IBufferWriter<byte> output, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null: WriteByte(output, (byte)Tag.Null); break;
            case JsonValueKind.False: WriteByte(output, (byte)Tag.False); break;
            case JsonValueKind.True: WriteByte(output, (byte)Tag.True); break;
            case JsonValueKind.Number:
                WriteByte(output, (byte)Tag.Number); WriteString(output, element.GetRawText()); break;
            case JsonValueKind.String:
                WriteByte(output, (byte)Tag.String); WriteString(output, element.GetString()!); break;
            case JsonValueKind.Array:
                WriteByte(output, (byte)Tag.Array); WriteInt32(output, element.GetArrayLength());
                foreach (var item in element.EnumerateArray()) WriteElement(output, item);
                break;
            case JsonValueKind.Object:
                WriteByte(output, (byte)Tag.Object);
                var properties = element.EnumerateObject().ToArray();
                WriteInt32(output, properties.Length);
                foreach (var property in properties) { WriteString(output, property.Name); WriteElement(output, property.Value); }
                break;
            default: throw new JsonException("Unsupported JSON token.");
        }
    }

    private static void WriteByte(IBufferWriter<byte> output, byte value) { var span = output.GetSpan(1); span[0] = value; output.Advance(1); }
    private static void WriteInt32(IBufferWriter<byte> output, int value) { var span = output.GetSpan(4); BinaryPrimitives.WriteInt32LittleEndian(span, value); output.Advance(4); }
    private static void WriteString(IBufferWriter<byte> output, string value)
    {
        var length = Utf8.GetByteCount(value); WriteInt32(output, length);
        var span = output.GetSpan(length); Utf8.GetBytes(value, span); output.Advance(length);
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _source; private int _offset;
        public Reader(ReadOnlySpan<byte> source) { _source = source; _offset = 0; }
        public bool End => _offset == _source.Length;
        public void WriteValue(Utf8JsonWriter writer)
        {
            var tag = (Tag)Byte();
            switch (tag)
            {
                case Tag.Null: writer.WriteNullValue(); break;
                case Tag.False: writer.WriteBooleanValue(false); break;
                case Tag.True: writer.WriteBooleanValue(true); break;
                case Tag.Number: writer.WriteRawValue(String(), skipInputValidation: false); break;
                case Tag.String: writer.WriteStringValue(String()); break;
                case Tag.Array:
                    writer.WriteStartArray(); for (var count = Count(); count > 0; count--) WriteValue(writer); writer.WriteEndArray(); break;
                case Tag.Object:
                    writer.WriteStartObject();
                    for (var count = Count(); count > 0; count--) { writer.WritePropertyName(String()); WriteValue(writer); }
                    writer.WriteEndObject(); break;
                default: throw new StorageFormatException("Native JSON contains an unknown token.");
            }
        }
        private byte Byte() { Require(1); return _source[_offset++]; }
        private int Count() { Require(4); var value = BinaryPrimitives.ReadInt32LittleEndian(_source[_offset..]); _offset += 4; if (value < 0) throw new StorageFormatException("Native JSON count is negative."); return value; }
        private string String() { var length = Count(); Require(length); var value = Utf8.GetString(_source.Slice(_offset, length)); _offset += length; return value; }
        private void Require(int count) { if (_offset > _source.Length - count) throw new StorageFormatException("Native JSON is truncated."); }
    }
}
