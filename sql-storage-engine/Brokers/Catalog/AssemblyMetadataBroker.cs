// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.Catalog;
internal sealed class AssemblyMetadataBroker : IAssemblyMetadataBroker
{
    public bool IsAssemblyVersion(string version) =>
        Version.TryParse(input: version, result: out _);
}