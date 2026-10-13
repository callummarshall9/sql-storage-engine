// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Processings.CheckpointStates;
internal interface ICheckpointStateCodecProcessingService
{
    byte[] WriteCheckpointState(CheckpointState checkpointState);

    CheckpointState ReadCheckpointState(ReadOnlySpan<byte> source);

    ulong ReadCheckpointStateRetention(CheckpointState checkpointState);
}