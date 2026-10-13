// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Services.Processings.CheckpointStates;

namespace sql_storage_engine.Exposures.Logging;
public static class CheckpointStateExposure
{
    private static readonly ICheckpointStateCodecProcessingService service = new CheckpointStateCodecProcessingService();
    public static byte[] WriteCheckpointState(CheckpointState checkpointState) =>
        service.WriteCheckpointState(checkpointState: checkpointState);

    public static CheckpointState ReadCheckpointState(ReadOnlySpan<byte> source) =>
        service.ReadCheckpointState(source: source);

    public static sql_storage_engine.Models.Identifiers.LogSequenceNumber ReadCheckpointStateRetention(CheckpointState checkpointState) =>
        new sql_storage_engine.Models.Identifiers.LogSequenceNumber
    {
        Value = service.ReadCheckpointStateRetention(checkpointState: checkpointState)
    };
}