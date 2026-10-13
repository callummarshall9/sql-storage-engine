// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Diagnostics;

namespace sql_storage_engine.Services.Orchestrations.WriteDamage;
internal interface IWriteDamageOrchestrationService
{
    byte[] RecoverPage(ReadOnlySpan<byte> page, ReadOnlySpan<byte> verifiedFullPageImage);

    WalDamagePolicy ClassifyWal(ReadOnlySpan<byte> bytes);
}