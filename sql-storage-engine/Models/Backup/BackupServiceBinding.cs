// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Backup;
public sealed class BackupServiceBinding
{
    public object? Source { get; init; }
    public BackupConfiguration? Configuration { get; init; }
}