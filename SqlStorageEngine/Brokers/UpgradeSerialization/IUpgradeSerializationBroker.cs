// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.UpgradeSerialization;
internal interface IUpgradeSerializationBroker
{
    byte[] CalculateChecksum(ReadOnlySpan<byte> bytes);

    ValueTask<(byte[]? Bytes, string? Error)> ReadBoundedAsync(string path, int expectedLength, CancellationToken cancellationToken);
}