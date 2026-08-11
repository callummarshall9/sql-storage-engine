using System.IO.Compression;
using System.Text;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Rows;

/// <summary>A compact, versioned binary XML payload used for native XML row storage.</summary>
internal static class SqlXmlBinaryCodec
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public static byte[] Encode(string xml)
    {
        using var output = new MemoryStream();
        output.WriteByte(1);
        using (var compressor = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true))
        { var bytes = Utf8.GetBytes(xml); compressor.Write(bytes); }
        return output.ToArray();
    }
    public static string Decode(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty || value[0] != 1) throw new StorageFormatException("Unsupported native XML format version.");
        try
        {
            using var input = new MemoryStream(value[1..].ToArray(), writable: false);
            using var decompressor = new BrotliStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            decompressor.CopyTo(output);
            return Utf8.GetString(output.ToArray());
        }
        catch (Exception exception) when (exception is InvalidDataException or DecoderFallbackException)
        { throw new StorageFormatException("Invalid native XML payload.", exception); }
    }
}
