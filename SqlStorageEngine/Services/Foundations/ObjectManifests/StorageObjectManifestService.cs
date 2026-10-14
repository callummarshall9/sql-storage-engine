// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Strategies;
using sql_storage_engine.Brokers.ObjectManifestCodecs;

namespace sql_storage_engine.Services.Foundations.ObjectManifests;
internal sealed partial class StorageObjectManifestService : IStorageObjectManifestService
{
    private readonly IObjectManifestCodecBroker broker = new ObjectManifestCodecBroker();
    public StorageStrategyContext EncodeStorageStrategyContext(StorageStrategyContext storageStrategyContext) =>
        TryCatch(operation: () =>
    {
        ValidateStorageStrategyContextEncode(storageStrategyContext: storageStrategyContext);
        return storageStrategyContext with { Bytes = EncodeStorageObjectManifest(storageObjectManifest: storageStrategyContext.Manifest!) };
    });

    public StorageStrategyContext DecodeStorageStrategyContext(StorageStrategyContext storageStrategyContext) =>
        TryCatch(operation: () =>
    {
        ValidateStorageStrategyContextDecode(storageStrategyContext: storageStrategyContext);
        var manifest = DecodeStorageObjectManifest(bytes: storageStrategyContext.Bytes!);
        ValidateStorageObjectManifest(storageObjectManifest: manifest);
        return storageStrategyContext with { Manifest = manifest };
    });
}