// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Foundations.FuzzManifests;
internal interface IFuzzManifestService
{
    void DecodeFuzzManifest(ReadOnlyMemory<byte> source);
}