// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Security;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Brokers.SecuritySerialization;

namespace sql_storage_engine.Services.Foundations.SecurityAuditRecords;
internal sealed partial class SecurityAuditRecordService(SecuritySerializationBroker securitySerializationBroker) : ISecurityAuditRecordService
{
    public void ValidateStorageAuditRecord(StorageAuditRecord storageAuditRecord) =>
        TryCatch(operation: () =>
    {
        ValidateStorageAuditRecordInput(storageAuditRecord: storageAuditRecord);
        var record = storageAuditRecord;
        ArgumentNullException.ThrowIfNull(argument: record);

        if (record.OperationId == Guid.Empty || record.CorrelationId == Guid.Empty || record.Revision <= 0 || record.ObjectId == Guid.Empty || !Enum.IsDefined(value: record.Action) || !Enum.IsDefined(value: record.Kind) || !Enum.IsDefined(value: record.Reason))
        {
            throw new ArgumentException("Invalid audit record.");
        }

        if (record.Principal is { } principal)
        {
            ValidatePrincipal(principal: principal);
        }

        if (record.TargetPrincipal is { } target)
        {
            ValidatePrincipal(principal: target);
        }

        if ((record.TargetPrincipal is null) != (record.PrincipalOperation is null) || record.PrincipalOperation is { } operation && (!Enum.IsDefined(value: operation) || record.Principal is null || record.Action != StoragePermissionAction.ManagePrincipals || record.Kind != StorageAuditKind.SecurityChange))
        {
            throw new ArgumentException("Invalid principal lifecycle audit.");
        }

        if (securitySerializationBroker.Serialize(value: record)
            .Length > 2048)
        {
            throw new ArgumentException("Audit record exceeds quota.");
        }
    });

    private static void ValidatePrincipal(StoragePrincipalId principal)
    {
        if (principal.Issuer == Guid.Empty || principal.Subject == Guid.Empty)
        {
            throw new ArgumentException("A principal needs nonempty issuer and subject identities.");
        }
    }

    internal static SecurityAuditRecordService CreateSecurityAuditRecordService() =>
        new(securitySerializationBroker: new sql_storage_engine.Brokers.SecuritySerialization.SecuritySerializationBroker());
}