// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Logging;
internal sealed class CheckpointReferenceState
{
    private object? sync;
    public object Sync { get => sync!; init => sync = value; }

    public ulong? Latest;
    public bool FailPublish;
}