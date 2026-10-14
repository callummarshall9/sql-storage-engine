// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Brokers.UpgradeSerialization;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.UpgradeStates;
internal sealed partial class UpgradeStateService : IUpgradeStateService
{
    private const int HeaderLength = 16;
    private const int PayloadLength = 12;
    private const int ChecksumLength = 32;
    private const int EncodedLength = HeaderLength + PayloadLength + ChecksumLength;
    private readonly UpgradeSerializationBroker serialization = new UpgradeSerializationBroker();
    public UpgradeContext DecodeUpgradeContext(UpgradeContext upgradeContext) =>
        TryCatch(operation: () =>
    {
        ValidateUpgradeContextDecode(upgradeContext: upgradeContext);

        return upgradeContext with
        {
            Progress = DecodeProgress(bytes: upgradeContext.Bytes!, targetVersion: upgradeContext.TargetVersion)
        };
    });

    public ValueTask<UpgradeContext> ReadUpgradeContextAsync(UpgradeContext upgradeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUpgradeContextRead(upgradeContext: upgradeContext, cancellationToken: cancellationToken);

        var read = await serialization.ReadBoundedAsync(path: upgradeContext.DatabasePath + ".upgrade.bin", expectedLength: EncodedLength, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (read.Error is not null)
        {
            throw new StorageFormatException(message: read.Error);
        }

        return upgradeContext with
        {
            Progress = DecodeProgress(bytes: read.Bytes!, targetVersion: upgradeContext.TargetVersion)
        };
    });

    public UpgradeContext EncodeUpgradeContext(UpgradeContext upgradeContext) =>
        TryCatch(operation: () =>
    {
        ValidateUpgradeContextEncode(upgradeContext: upgradeContext);
        var progress = upgradeContext.Progress!;
        var bytes = new byte[EncodedLength];
        "UPGRSTAT"u8.CopyTo(destination: bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 8), value: 1);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 10), value: 1);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes.AsSpan(start: 12), value: PayloadLength);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: HeaderLength), value: progress.SourceVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: HeaderLength + 2), value: progress.TargetVersion);
        BinaryPrimitives.WriteInt32LittleEndian(destination: bytes.AsSpan(start: HeaderLength + 4), value: progress.NextStep);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes.AsSpan(start: HeaderLength + 8), value: checked((uint)progress.Status));

        serialization.CalculateChecksum(bytes: bytes.AsSpan(start: 0, length: HeaderLength + PayloadLength))
            .CopyTo(array: bytes, index: HeaderLength + PayloadLength);

        return upgradeContext with
        {
            Bytes = bytes
        };
    });

    private UpgradeProgress DecodeProgress(ReadOnlySpan<byte> bytes, ushort targetVersion)
    {
        if (bytes.Length != EncodedLength)
        {
            throw new StorageFormatException(message: "Upgrade progress has an invalid encoded length.");
        }

        if (!bytes[..8].SequenceEqual(other: "UPGRSTAT"u8))
        {
            throw new StorageFormatException(message: "Upgrade progress magic is invalid.");
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(source: bytes[8..]) != 1 || BinaryPrimitives.ReadUInt16LittleEndian(source: bytes[10..]) != 1)
        {
            throw new StorageFormatException(message: "Upgrade progress format version or record kind is unsupported.");
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(source: bytes[12..]) != PayloadLength)
        {
            throw new StorageFormatException(message: "Upgrade progress payload length is invalid.");
        }

        var checksum = serialization.CalculateChecksum(bytes: bytes[..(HeaderLength + PayloadLength)]);

        if (!bytes[(HeaderLength + PayloadLength)..].SequenceEqual(other: checksum))
        {
            throw new StorageFormatException(message: "Upgrade progress checksum is invalid.");
        }

        var progress = new UpgradeProgress
        {
            SourceVersion = BinaryPrimitives.ReadUInt16LittleEndian(source: bytes[HeaderLength..]),
            TargetVersion = BinaryPrimitives.ReadUInt16LittleEndian(source: bytes[(HeaderLength + 2)..]),
            NextStep = BinaryPrimitives.ReadInt32LittleEndian(source: bytes[(HeaderLength + 4)..]),
            Status = (UpgradeStatus)BinaryPrimitives.ReadUInt32LittleEndian(source: bytes[(HeaderLength + 8)..])
        };

        ValidateUpgradeProgress(upgradeProgress: progress, targetVersion: targetVersion);
        return progress;
    }
}