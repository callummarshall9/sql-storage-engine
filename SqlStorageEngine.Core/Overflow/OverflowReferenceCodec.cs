using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Overflow;

/// <summary>Encodes fixed-width references from rows to exclusively owned overflow chains.</summary>
public static class OverflowReferenceCodec
{
    public const int EncodedLength = 16;
    public const long MaximumValueLength = int.MaxValue;
    // Enough pages for a 2GB SQL LOB even with the minimum supported 4KB page size.
    public const int MaximumChainLength = 530_505;

    public static void Write(Span<byte> destination, OverflowReference reference)
    {
        if (destination.Length < EncodedLength) throw new ArgumentException("Overflow reference destination is truncated.", nameof(destination));
        Validate(reference);
        BinaryPrimitives.WriteUInt64LittleEndian(destination, reference.FirstPageId.Value);
        BinaryPrimitives.WriteInt64LittleEndian(destination[8..], reference.TotalLength);
    }

    public static OverflowReference Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < EncodedLength) throw new StorageFormatException("Overflow reference is truncated.");
        var reference = new OverflowReference(new PageId(BinaryPrimitives.ReadUInt64LittleEndian(source)),
            BinaryPrimitives.ReadInt64LittleEndian(source[8..]));
        Validate(reference);
        return reference;
    }

    public static void Validate(OverflowReference reference)
    {
        if (reference.FirstPageId.Value == 0) throw new StorageFormatException("Overflow chains cannot begin at page zero.");
        if (reference.TotalLength <= 0 || reference.TotalLength > MaximumValueLength)
            throw new StorageFormatException($"Overflow length must be between 1 and {MaximumValueLength} bytes.");
    }
}
