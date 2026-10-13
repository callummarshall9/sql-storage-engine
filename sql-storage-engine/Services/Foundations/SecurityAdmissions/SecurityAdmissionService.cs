// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Security;

namespace sql_storage_engine.Services.Foundations.SecurityAdmissions;
internal sealed partial class SecurityAdmissionService : ISecurityAdmissionService
{
    public SecurityAdmission PrepareSecurityAdmission(SecurityAdmission securityAdmission) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityAdmission(securityAdmission: securityAdmission);
        var request = securityAdmission.Request!;

        var requestedAccess = (request.AccessSource ?? request.Access).Take(count: 257)
            .ToArray();

        request = new StorageAuthorizationRequest
        {
            Principal = request.Principal,
            Revision = request.Revision,
            OperationId = request.OperationId,
            CorrelationId = request.CorrelationId,
            Access = Array.AsReadOnly(array: requestedAccess)
        };

        ValidatePrincipal(principal: request.Principal);
        var state = Current(securityAdmission: securityAdmission, expected: request.Revision, operation: request.OperationId);

        if (request.CorrelationId == Guid.Empty || request.Access.Count is < 1 or > 256)
        {
            throw new ArgumentException("Invalid authorization request.");
        }

        if (!state.Principals.Any(predicate: p => p.Id == request.Principal && p.Active))
        {
            throw new StorageAuthorizationException("Principal is not active.");
        }

        foreach (var access in request.Access)
        {
            if (access is null || access.Object is null || !Enum.IsDefined(value: access.Action) || access.Action == StoragePermissionAction.ManagePrincipals)
            {
                throw new ArgumentException("Invalid required access.");
            }

            if (access.Object.DatabaseId != securityAdmission.DatabaseId || !securityAdmission.Tables!.Any(predicate: t => t.ObjectId == access.Object.ObjectId && t.SchemaVersion == access.Object.SchemaVersion))
            {
                throw new StorageAuthorizationException("Object binding is stale or foreign.");
            }

            var matches = state.Permissions.Where(predicate: p => p.Principal == request.Principal && p.ObjectId == access.Object.ObjectId && p.Action == access.Action)
            .ToArray();

            if (!matches.Any(predicate: p => p.Effect == StoragePermissionEffect.Grant) || matches.Any(predicate: p => p.Effect == StoragePermissionEffect.Deny))
            {
                throw new StorageAuthorizationException("Required permission is denied.");
            }
        }

        var read = request.Access.All(predicate: a => a.Action == StoragePermissionAction.Select);
        var first = request.Access[0];

        var record = new sql_storage_engine.Models.Security.StorageAuditRecord
        {
            OperationId = request.OperationId,
            CorrelationId = request.CorrelationId,
            Principal = request.Principal,
            ObjectId = request.Access.Select(selector: a => a.Object.ObjectId)
            .Distinct()
            .Count() == 1 ? first.Object.ObjectId : null,
            Action = read ? StoragePermissionAction.Select : request.Access.First(predicate: a => a.Action != StoragePermissionAction.Select)
            .Action,
            Kind = read ? StorageAuditKind.ReadAdmission : StorageAuditKind.Mutation,
            Revision = state.Revision
        };

        return securityAdmission with
        {
            Request = request,
            Record = record,
            ReadOnly = read
        };
    });

    private static StorageSecurityState Current(SecurityAdmission securityAdmission, long expected, Guid operation)
    {
        var state = securityAdmission.State!;

        if (expected != state.Revision)
        {
            throw new StorageAuthorizationException("Security revision is stale.");
        }

        if (operation == Guid.Empty || state.Audit.Any(predicate: a => a.OperationId == operation))
        {
            throw new ArgumentException("Invalid or previously recorded operation identity; resolve before retrying.");
        }

        return state;
    }

    private static void ValidatePrincipal(StoragePrincipalId principal)
    {
        if (principal.Issuer == Guid.Empty || principal.Subject == Guid.Empty)
        {
            throw new ArgumentException(message: "A principal needs nonempty issuer and subject identities.");
        }
    }
}