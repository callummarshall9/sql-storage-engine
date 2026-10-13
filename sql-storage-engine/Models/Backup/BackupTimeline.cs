// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Backup;
internal sealed class BackupTimeline
{
    public System.Guid DatabaseId { get; set; }
    public ulong Timeline { get; set; }
    public ulong ParentTimeline { get; set; }
    public ulong ForkLsn { get; set; }
}