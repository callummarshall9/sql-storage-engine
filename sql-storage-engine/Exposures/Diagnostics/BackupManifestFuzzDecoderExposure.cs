// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Services.Foundations.FuzzManifests;

namespace sql_storage_engine.Exposures.Diagnostics;
public static class BackupManifestFuzzDecoderExposure
{
    private static readonly IFuzzManifestService service = new FuzzManifestService();
    public static void DecodeFuzzInput(ReadOnlyMemory<byte> source) =>
        service.DecodeFuzzManifest(source: source);
}