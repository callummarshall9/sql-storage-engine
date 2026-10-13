// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Backup;
public sealed class BackupConfiguration
{
    public System.TimeProvider? TimeProviderSource { get; set; }
    public sql_storage_engine.Models.Retention.WalRetentionServiceBinding? RetentionBinding { get; set; }
    public ulong? SourceRegistrationId { get; set; }
    public System.Func<ulong>? CurrentDurableLsnSource { get; set; }
}