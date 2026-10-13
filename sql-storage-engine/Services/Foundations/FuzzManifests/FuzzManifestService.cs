// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Brokers.SecuritySerialization;
using sql_storage_engine.Models.Backup;

namespace sql_storage_engine.Services.Foundations.FuzzManifests;
internal sealed partial class FuzzManifestService : IFuzzManifestService
{
    private readonly SecuritySerializationBroker serialization = new SecuritySerializationBroker();
    public void DecodeFuzzManifest(ReadOnlyMemory<byte> source) =>
        TryCatch(operation: () =>
    {
        ValidateFuzzManifest(source: source);
        _ = serialization.Deserialize<BackupManifest>(bytes: source.ToArray());
    });
}