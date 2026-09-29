using System.Security.Cryptography;
using SqlExecutionEngine.Storage.Abstractions;

namespace SqlStorageEngine.Core;

/// <summary>Private, versioned adapter format; never an engine SQL catalog or portable export format.</summary>
internal static class PageStateCodec
{
    internal const int MaximumBytes = 9 * 1024 * 1024;
    private const int Magic = 0x31434750;

    internal static byte[] Encode(PageBackendState state)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic); writer.Write(state.Id.Value.ToByteArray());
        writer.Write(state.Records.Count);
        foreach (var pair in state.Records)
        {
            writer.Write(pair.Key.Collection.Value.ToByteArray());
            Bytes(pair.Key.Key); Bytes(pair.Value);
        }
        writer.Write(state.Receipts.Count);
        foreach (var pair in state.Receipts)
        {
            writer.Write(pair.Key.Value.ToByteArray()); writer.Write(pair.Value.Fingerprint.ToArray());
            writer.Write((byte)pair.Value.Outcome.Status);
            if (pair.Value.Outcome.Generation is { } generation) writer.Write(generation.Token.ToArray());
        }
        if (stream.Length + 32 > MaximumBytes) throw new BackendResourceException();
        var payload = stream.ToArray();
        writer.Write(SHA256.HashData(payload));
        return stream.ToArray();
        void Bytes(ByteString value) { writer.Write(value.Length); writer.Write(value.ToArray()); }
    }

    internal static PageBackendState Decode(byte[] bytes)
    {
        if (bytes.Length is < 60 or > MaximumBytes ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32)), bytes.AsSpan(bytes.Length - 32)))
            throw new InvalidDataException("Invalid page adapter state.");
        using var stream = new MemoryStream(bytes, 0, bytes.Length - 32, false);
        using var reader = new BinaryReader(stream);
        if (reader.ReadInt32() != Magic) throw new InvalidDataException("Unsupported page adapter format.");
        var state = new PageBackendState(new(new Guid(Exact(16))));
        var count = Count(8 * 1024 * 1024 / 26);
        for (var i = 0; i < count; i++)
        {
            var collection = new CollectionId(new Guid(Exact(16)));
            var key = Bytes(256); var value = Bytes(1048576);
            if (key.Length == 0 || !state.Records.TryAdd(new(collection, key), value)) throw new InvalidDataException();
        }
        count = Count(1024);
        for (var i = 0; i < count; i++)
        {
            var operation = new OperationId(new Guid(Exact(16)));
            var fingerprint = new ByteString(Exact(32)); var status = (CommitStatus)reader.ReadByte();
            if (status is not (CommitStatus.Committed or CommitStatus.Conflict or CommitStatus.Unknown)) throw new InvalidDataException();
            var receipt = new CommitReceipt(state.Id, operation, status,
                status == CommitStatus.Committed ? new(new(Exact(16))) : null);
            if (!state.Receipts.TryAdd(operation, new(fingerprint, receipt))) throw new InvalidDataException();
        }
        if (stream.Position != stream.Length || state.RecordBytes > 8 * 1024 * 1024) throw new InvalidDataException();
        return state;
        int Count(int maximum)
        {
            var value = reader.ReadInt32();
            if (value < 0 || value > maximum || value > stream.Length - stream.Position) throw new InvalidDataException();
            return value;
        }
        byte[] Exact(int count)
        {
            var result = reader.ReadBytes(count);
            if (result.Length != count) throw new InvalidDataException();
            return result;
        }
        ByteString Bytes(int maximum) => new(Exact(Count(maximum)));
    }
}
