// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Security;
public readonly record struct StoragePrincipalId
{
    public Guid Issuer { get; init; }
    public Guid Subject { get; init; }
}