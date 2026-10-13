// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using System.Text.Json;
using sql_storage_engine.Storage;
using sql_storage_engine.Security;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Brokers.SecuritySerialization;

namespace sql_storage_engine.Services.Foundations.SecurityStates;
internal sealed partial class SecurityStateService(SecuritySerializationBroker securitySerializationBroker) : ISecurityStateService
{
    public SecurityStatePayload CreateSecurityStatePayload(SecurityStatePayload securityStatePayload) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityStatePayload(securityStatePayload: securityStatePayload);

        return securityStatePayload with
        {
            State = new StorageSecurityState
            {
                Revision = 1,
                Principals = [],
                Permissions = [],
                Audit = [],
                RetiredPrincipals = [],
                DatabasePermissions = [],
                PrincipalDependencies = []
            }
        };
    });

    public byte[] EncodeSecurityStatePayload(SecurityStatePayload securityStatePayload) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityStatePayload(securityStatePayload: securityStatePayload);
        ArgumentNullException.ThrowIfNull(argument: securityStatePayload.State);
        return securitySerializationBroker.Serialize(value: securityStatePayload.State);
    });

    public SecurityStatePayload DecodeSecurityStatePayload(SecurityStatePayload securityStatePayload) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityStatePayload(securityStatePayload: securityStatePayload);
        var bytes = securityStatePayload.Bytes;

        try
        {
            if (bytes.Length > 4 * 1024 * 1024)
            {
                throw new ArgumentException();
            }

            var state = securitySerializationBroker.Deserialize<StorageSecurityState>(bytes: bytes) ?? throw new ArgumentException();

            state = state with
            {
                Revision = securitySerializationBroker.HasProperty(bytes: bytes, name: "Revision") ? state.Revision : 1,
                Principals = securitySerializationBroker.HasProperty(bytes: bytes, name: "Principals") ? state.Principals : [],
                Permissions = securitySerializationBroker.HasProperty(bytes: bytes, name: "Permissions") ? state.Permissions : [],
                Audit = securitySerializationBroker.HasProperty(bytes: bytes, name: "Audit") ? state.Audit : [],
                RetiredPrincipals = securitySerializationBroker.HasProperty(bytes: bytes, name: "RetiredPrincipals") ? state.RetiredPrincipals : [],
                DatabasePermissions = securitySerializationBroker.HasProperty(bytes: bytes, name: "DatabasePermissions") ? state.DatabasePermissions : [],
                PrincipalDependencies = securitySerializationBroker.HasProperty(bytes: bytes, name: "PrincipalDependencies") ? state.PrincipalDependencies : []
            };

            if (state.Revision <= 0 || state.Principals is null || state.Permissions is null || state.Audit is null || state.Principals.Length > 1024 || state.Permissions.Length > 1024 || state.Audit.Length > 4096 || state.Principals.Select(selector: p => p.Id)
            .Distinct()
            .Count() != state.Principals.Length || state.Permissions.Distinct()
            .Count() != state.Permissions.Length || state.Audit.Select(selector: a => a.OperationId)
            .Distinct()
            .Count() != state.Audit.Length)
            {
                throw new ArgumentException();
            }

            ValidateLifecycle(state: state);

            foreach (var principal in state.Principals)
            {
                ValidatePrincipal(principal: principal.Id);
            }

            foreach (var permission in state.Permissions)
            {
                ValidatePrincipal(principal: permission.Principal);

                if (permission.ObjectId == Guid.Empty || !Enum.IsDefined(value: permission.Action) || permission.Action == StoragePermissionAction.ManagePrincipals || !Enum.IsDefined(value: permission.Effect))
                {
                    throw new ArgumentException();
                }
            }

            foreach (var audit in state.Audit)
            {
                ValidateRecord(record: audit);

                if (audit.Kind is not (StorageAuditKind.Mutation or StorageAuditKind.SecurityChange) || audit.Revision > state.Revision)
                {
                    throw new ArgumentException();
                }
            }

            return securityStatePayload with
            {
                State = state
            };
        }
        catch (Exception error)when (error is JsonException or ArgumentException or NullReferenceException)
        {
            throw new StorageFormatException("Invalid security catalog.");
        }
    });

    private void ValidateRecord(StorageAuditRecord record)
    {
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
    }

    private static void ValidatePrincipal(StoragePrincipalId principal)
    {
        if (principal.Issuer == Guid.Empty || principal.Subject == Guid.Empty)
        {
            throw new ArgumentException("A principal needs nonempty issuer and subject identities.");
        }
    }

    private static void ValidateLifecycle(StorageSecurityState state)
    {
        if (state.RetiredPrincipals is null || state.DatabasePermissions is null || state.PrincipalDependencies is null || state.RetiredPrincipals.Length > 1024 || state.DatabasePermissions.Length > 1024 || state.PrincipalDependencies.Length > 1024 || state.RetiredPrincipals.Distinct()
            .Count() != state.RetiredPrincipals.Length || state.DatabasePermissions.Distinct()
            .Count() != state.DatabasePermissions.Length || state.PrincipalDependencies.Distinct()
            .Count() != state.PrincipalDependencies.Length)
        {
            throw new ArgumentException("Invalid principal lifecycle catalog.");
        }

        foreach (var id in state.RetiredPrincipals)
        {
            ValidatePrincipal(principal: id);

            if (state.Principals.Any(predicate: p => p.Id == id) || state.Permissions.Any(predicate: p => p.Principal == id))
            {
                throw new ArgumentException("Retired principal retains authority.");
            }
        }

        foreach (var permission in state.DatabasePermissions)
        {
            if (!Enum.IsDefined(value: permission.Action) || !Enum.IsDefined(value: permission.Effect) || !state.Principals.Any(predicate: p => p.Id == permission.Principal))
            {
                throw new ArgumentException("Invalid database permission.");
            }
        }

        foreach (var dependency in state.PrincipalDependencies)
        {
            if (!Enum.IsDefined(value: dependency.Kind) || dependency.ReferenceId == Guid.Empty || !state.Principals.Any(predicate: p => p.Id == dependency.Principal))
            {
                throw new ArgumentException("Invalid principal dependency.");
            }
        }
    }

    internal static SecurityStateService CreateSecurityStateService() =>
        new SecurityStateService(securitySerializationBroker: new sql_storage_engine.Brokers.SecuritySerialization.SecuritySerializationBroker());
}