// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecurityAuditRecords;
internal interface ISecurityAuditRecordService
{
    void ValidateStorageAuditRecord(StorageAuditRecord storageAuditRecord);
}