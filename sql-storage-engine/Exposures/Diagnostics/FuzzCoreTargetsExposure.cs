// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Exposures.Heap;
using sql_storage_engine.Exposures.Catalog;
using sql_storage_engine.Exposures.Indexes;
using sql_storage_engine.Exposures.Overflow;
using sql_storage_engine.Exposures.Logging;
using sql_storage_engine.Services.Orchestrations.FuzzCoreRegistries;

namespace sql_storage_engine.Exposures.Diagnostics;
public sealed class FuzzCoreTargetsExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private readonly IFuzzCoreRegistryOrchestrationService service = new FuzzCoreRegistryOrchestrationService();
    private readonly FuzzCoreRegistry registry;
    public FuzzCoreTargetsExposure()
    {
        registry = new FuzzCoreRegistry
        {
            Decoders = [DatabaseHeaderExposure.DecodeFuzzInput, PageHeaderExposure.DecodeFuzzInput, HeapPageLayoutExposure.DecodeFuzzInput, CatalogCodecExposure.DecodeFuzzInput, IndexKeyExposure.DecodeFuzzInput, CatalogCodecExposure.DecodeFuzzInput, OverflowPageHeaderExposure.DecodeFuzzInput, LeafIndexPageExposure.DecodeFuzzInput, WalReadResultExposure.DecodeFuzzInput, BackupManifestFuzzDecoderExposure.DecodeFuzzInput]
        };
    }

    public IReadOnlyList<FuzzTarget> ReadTargets() =>
        service.CreateFuzzCoreRegistry(fuzzCoreRegistry: registry)
            .Targets!;
}