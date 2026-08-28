using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine;

internal static class GraphIdentityCodec
{
    internal const int EncodedLength = 50;
    private const byte FormatVersion = 1;

    internal static SqlValue Encode(byte kind, DatabaseId databaseId, TableId tableId, ulong schemaVersion,
        Guid value)
    {
        Validate(databaseId, tableId, schemaVersion, value);
        Span<byte> bytes = stackalloc byte[EncodedLength];
        bytes[0] = FormatVersion;
        bytes[1] = kind;
        databaseId.Value.TryWriteBytes(bytes[2..18]);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[18..26], tableId.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[26..34], schemaVersion);
        value.TryWriteBytes(bytes[34..50]);
        return SqlValue.Binary(bytes);
    }

    internal static (DatabaseId DatabaseId, TableId TableId, ulong SchemaVersion, Guid Value) Decode(
        SqlValue value, byte expectedKind, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value is not BinarySqlValue binary || binary.Value.Length != EncodedLength)
            throw new ArgumentException($"A graph identity must be binary({EncodedLength}).", parameterName);
        var bytes = binary.Value.Span;
        if (bytes[0] != FormatVersion || bytes[1] != expectedKind)
            throw new ArgumentException("The graph identity has an unsupported format or entity kind.", parameterName);
        var result = (new DatabaseId(new Guid(bytes[2..18])),
            new TableId(BinaryPrimitives.ReadUInt64LittleEndian(bytes[18..26])),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes[26..34]), new Guid(bytes[34..50]));
        try { Validate(result.Item1, result.Item2, result.Item3, result.Item4); }
        catch (ArgumentException exception)
        { throw new ArgumentException("The graph identity contains invalid ownership metadata.", parameterName, exception); }
        return result;
    }

    private static void Validate(DatabaseId databaseId, TableId tableId, ulong schemaVersion, Guid value)
    {
        if (databaseId.Value == Guid.Empty) throw new ArgumentException("A graph identity requires a database ID.", nameof(databaseId));
        if (tableId.Value == 0) throw new ArgumentException("A graph identity requires a table ID.", nameof(tableId));
        if (schemaVersion == 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (value == Guid.Empty) throw new ArgumentException("A graph identity value cannot be empty.", nameof(value));
    }
}
