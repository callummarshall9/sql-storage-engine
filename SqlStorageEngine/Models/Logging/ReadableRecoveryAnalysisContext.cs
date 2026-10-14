// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Logging;
internal readonly ref struct ReadableRecoveryAnalysisContext
{
    public RecoveryAnalysis recoveryAnalysis { get; init; }
    public Guid expectedDatabaseId { get; init; }
    public ulong expectedTimeline { get; init; }
    public ReadOnlySpan<byte> recordBytes { get; init; }
}