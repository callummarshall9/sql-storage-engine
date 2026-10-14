// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Dependencies.UpgradeSerialization;

namespace sql_storage_engine.Brokers.UpgradeSerialization;
internal sealed class UpgradeSerializationBroker : IUpgradeSerializationBroker
{
    public byte[] CalculateChecksum(ReadOnlySpan<byte> bytes)
    {
        using var serialization = new UpgradeSerializationDependency();
        return serialization.CalculateChecksum(bytes: bytes);
    }

    public async ValueTask<(byte[]? Bytes, string? Error)> ReadBoundedAsync(string path, int expectedLength, CancellationToken cancellationToken)
    {
        using var serialization = new UpgradeSerializationDependency();

        return await serialization.ReadBoundedAsync(path: path, expectedLength: expectedLength, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }
}