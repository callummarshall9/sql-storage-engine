// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Services.Foundations.SecurityAuditRecords;
using sql_storage_engine.Brokers.SecuritySerialization;
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Exposures.Security;
internal static class StorageAuditRecordExposure
{
    private static readonly ISecurityAuditRecordService service = new SecurityAuditRecordService(securitySerializationBroker: new SecuritySerializationBroker());
    internal static void ValidateRecord(StorageAuditRecord storageAuditRecord) =>
        service.ValidateStorageAuditRecord(storageAuditRecord: storageAuditRecord);
}