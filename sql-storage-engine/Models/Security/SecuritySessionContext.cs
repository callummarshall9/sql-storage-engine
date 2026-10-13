// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Security;
internal sealed record SecuritySessionContext
{
    public int Operation { get; init; }
    public StorageSecurityState? State { get; set; }
    public StorageSecuritySnapshot? Snapshot { get; set; }
    public SecurityCatalogMutation? Mutation { get; set; }
    public SecurityAdmission? Admission { get; set; }
    public SecurityEventContext? Events { get; set; }
    public StorageAuditRecord? Record { get; set; }
}