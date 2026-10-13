// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Security;
internal sealed record SecurityStatePayload
{
    public StorageSecurityState? State { get; set; }
    public ReadOnlyMemory<byte> Bytes { get; set; }
}