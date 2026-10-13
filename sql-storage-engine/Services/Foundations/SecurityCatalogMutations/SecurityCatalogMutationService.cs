// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Security;

namespace sql_storage_engine.Services.Foundations.SecurityCatalogMutations;
internal sealed partial class SecurityCatalogMutationService : ISecurityCatalogMutationService
{
    public SecurityCatalogMutation PrepareSecurityCatalogMutationPrincipal(SecurityCatalogMutation securityCatalogMutation) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityCatalogMutation(securityCatalogMutation: securityCatalogMutation);
        var principal = securityCatalogMutation.Principal!;
        var expectedRevision = securityCatalogMutation.ExpectedRevision;
        var operationId = securityCatalogMutation.OperationId;
        var remove = securityCatalogMutation.Remove;
        ArgumentNullException.ThrowIfNull(argument: principal);
        ValidatePrincipal(principal: principal.Id);
        var state = Current(securityCatalogMutation: securityCatalogMutation, expected: securityCatalogMutation.ExpectedRevision, operation: securityCatalogMutation.OperationId);

        if (state.RetiredPrincipals.Contains(value: principal.Id))
        {
            throw new InvalidOperationException("Principal identity is retired.");
        }

        var principals = state.Principals.Where(predicate: p => p.Id != principal.Id)
            .Append(element: principal)
            .ToArray();

        if (principals.Length > 1024)
        {
            throw new InvalidOperationException("Principal quota exceeded.");
        }

        var next = state with
        {
            Revision = state.Principals.Contains(value: principal) ? state.Revision : checked(state.Revision + 1),
            Principals = principals
        };

        return securityCatalogMutation with
        {
            State = next,
            Record = new sql_storage_engine.Models.Security.StorageAuditRecord
            {
                OperationId = operationId,
                CorrelationId = operationId,
                Principal = null,
                ObjectId = null,
                Action = StoragePermissionAction.ManagePermissions,
                Kind = StorageAuditKind.SecurityChange,
                Revision = next.Revision
            }
        };
    });

    public SecurityCatalogMutation PrepareSecurityCatalogMutationPermission(SecurityCatalogMutation securityCatalogMutation) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityCatalogMutation(securityCatalogMutation: securityCatalogMutation);
        var permission = securityCatalogMutation.Permission!;
        var expectedRevision = securityCatalogMutation.ExpectedRevision;
        var operationId = securityCatalogMutation.OperationId;
        var remove = securityCatalogMutation.Remove;
        ArgumentNullException.ThrowIfNull(argument: permission);
        ValidatePrincipal(principal: permission.Principal);
        var state = Current(securityCatalogMutation: securityCatalogMutation, expected: securityCatalogMutation.ExpectedRevision, operation: securityCatalogMutation.OperationId);

        if (!Enum.IsDefined(value: permission.Action) || permission.Action == StoragePermissionAction.ManagePrincipals || !Enum.IsDefined(value: permission.Effect) || !state.Principals.Any(predicate: p => p.Id == permission.Principal && p.Active) || !securityCatalogMutation.Tables!.Any(predicate: t => t.ObjectId == permission.ObjectId))
        {
            throw new ArgumentException("Invalid permission target.");
        }

        var permissions = state.Permissions.Where(predicate: p => p != permission)
            .Concat(second: remove ? [] : new[] { permission })
            .ToArray();

        if (permissions.Length > 1024)
        {
            throw new InvalidOperationException("Permission quota exceeded.");
        }

        var next = state with
        {
            Revision = state.Permissions.Contains(value: permission) != remove ? state.Revision : checked(state.Revision + 1),
            Permissions = permissions
        };

        return securityCatalogMutation with
        {
            State = next,
            Record = new sql_storage_engine.Models.Security.StorageAuditRecord
            {
                OperationId = operationId,
                CorrelationId = operationId,
                Principal = null,
                ObjectId = permission.ObjectId,
                Action = StoragePermissionAction.ManagePermissions,
                Kind = StorageAuditKind.SecurityChange,
                Revision = next.Revision
            }
        };
    });

    public SecurityCatalogMutation PrepareSecurityCatalogMutationDatabasePermission(SecurityCatalogMutation securityCatalogMutation) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityCatalogMutation(securityCatalogMutation: securityCatalogMutation);
        var permission = securityCatalogMutation.DatabasePermission!;
        var expectedRevision = securityCatalogMutation.ExpectedRevision;
        var operationId = securityCatalogMutation.OperationId;
        var remove = securityCatalogMutation.Remove;
        ArgumentNullException.ThrowIfNull(argument: permission);
        var state = Current(securityCatalogMutation: securityCatalogMutation, expected: securityCatalogMutation.ExpectedRevision, operation: securityCatalogMutation.OperationId);

        if (!Enum.IsDefined(value: permission.Action) || !Enum.IsDefined(value: permission.Effect) || !state.Principals.Any(predicate: p => p.Id == permission.Principal && p.Active))
        {
            throw new ArgumentException("Invalid database permission target.");
        }

        var entries = state.DatabasePermissions.Where(predicate: p => p != permission)
            .Concat(second: remove ? [] : new[] { permission })
            .ToArray();

        if (entries.Length > 1024)
        {
            throw new InvalidOperationException("Database permission quota exceeded.");
        }

        var next = state with
        {
            DatabasePermissions = entries,
            Revision = state.DatabasePermissions.Contains(value: permission) != remove ? state.Revision : checked(state.Revision + 1)
        };

        return securityCatalogMutation with
        {
            State = next,
            Record = new sql_storage_engine.Models.Security.StorageAuditRecord
            {
                OperationId = operationId,
                CorrelationId = operationId,
                Principal = null,
                ObjectId = securityCatalogMutation.DatabaseId.Value,
                Action = StoragePermissionAction.ManagePrincipals,
                Kind = StorageAuditKind.SecurityChange,
                Revision = next.Revision
            }
        };
    });

    public SecurityCatalogMutation PrepareSecurityCatalogMutationPrincipalDependency(SecurityCatalogMutation securityCatalogMutation) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityCatalogMutation(securityCatalogMutation: securityCatalogMutation);
        var dependency = securityCatalogMutation.PrincipalDependency!;
        var expectedRevision = securityCatalogMutation.ExpectedRevision;
        var operationId = securityCatalogMutation.OperationId;
        var remove = securityCatalogMutation.Remove;
        ArgumentNullException.ThrowIfNull(argument: dependency);
        var state = Current(securityCatalogMutation: securityCatalogMutation, expected: securityCatalogMutation.ExpectedRevision, operation: securityCatalogMutation.OperationId);

        if (!Enum.IsDefined(value: dependency.Kind) || dependency.ReferenceId == Guid.Empty || !state.Principals.Any(predicate: p => p.Id == dependency.Principal))
        {
            throw new ArgumentException("Invalid principal dependency.");
        }

        var entries = state.PrincipalDependencies.Where(predicate: p => p != dependency)
            .Concat(second: remove ? [] : new[] { dependency })
            .ToArray();

        if (entries.Length > 1024)
        {
            throw new InvalidOperationException("Principal dependency quota exceeded.");
        }

        var next = state with
        {
            PrincipalDependencies = entries,
            Revision = state.PrincipalDependencies.Contains(value: dependency) != remove ? state.Revision : checked(state.Revision + 1)
        };

        return securityCatalogMutation with
        {
            State = next,
            Record = new sql_storage_engine.Models.Security.StorageAuditRecord
            {
                OperationId = operationId,
                CorrelationId = operationId,
                Principal = null,
                ObjectId = securityCatalogMutation.DatabaseId.Value,
                Action = StoragePermissionAction.ManagePrincipals,
                Kind = StorageAuditKind.SecurityChange,
                Revision = next.Revision
            }
        };
    });

    public SecurityCatalogMutation PrepareSecurityCatalogMutationPrincipalChange(SecurityCatalogMutation securityCatalogMutation) =>
        TryCatch(operation: () =>
    {
        ValidateSecurityCatalogMutation(securityCatalogMutation: securityCatalogMutation);
        var change = securityCatalogMutation.PrincipalChange!;
        var expectedRevision = securityCatalogMutation.ExpectedRevision;
        var operationId = securityCatalogMutation.OperationId;
        var remove = securityCatalogMutation.Remove;
        ArgumentNullException.ThrowIfNull(argument: change);
        ValidatePrincipal(principal: change.Actor);
        ValidatePrincipal(principal: change.Target);
        var state = Current(securityCatalogMutation: securityCatalogMutation, expected: change.Revision, operation: change.OperationId);

        if (change.DatabaseId != securityCatalogMutation.DatabaseId || change.CorrelationId == Guid.Empty || !Enum.IsDefined(value: change.Operation))
        {
            throw new ArgumentException("Invalid principal lifecycle request.");
        }

        var authority = state.DatabasePermissions.Where(predicate: p => p.Principal == change.Actor && p.Action == StorageDatabasePermissionAction.ManagePrincipals)
            .ToArray();

        if (!state.Principals.Any(predicate: p => p.Id == change.Actor && p.Active) || !authority.Any(predicate: p => p.Effect == StoragePermissionEffect.Grant) || authority.Any(predicate: p => p.Effect == StoragePermissionEffect.Deny))
        {
            throw new StorageAuthorizationException("Database principal administration is denied.");
        }

        if (state.RetiredPrincipals.Contains(value: change.Target))
        {
            throw new InvalidOperationException("Principal identity is retired.");
        }

        var existing = state.Principals.SingleOrDefault(predicate: p => p.Id == change.Target);
        StorageSecurityState next;

        switch (change.Operation)
        {
            case StoragePrincipalOperation.Create:
                if (existing is not null)
                {
                    throw new InvalidOperationException("Principal already exists.");
                }

                if (state.Principals.Length >= 1024)
                {
                    throw new InvalidOperationException("Principal quota exceeded.");
                }

                next = state with
                {
                    Principals = [..state.Principals, new sql_storage_engine.Models.Security.StoragePrincipal
                    {
                        Id = change.Target,
                        Active = true
                    }

                    ],
                    Revision = checked(state.Revision + 1)
                };
                break;
            case StoragePrincipalOperation.Deactivate:
                if (existing is null)
                {
                    throw new InvalidOperationException("Principal does not exist.");
                }

                next = state with
                {
                    Principals = state.Principals.Select(selector: p => p.Id == change.Target ? p with { Active = false } : p)
            .ToArray(),
                    Revision = existing.Active ? checked(state.Revision + 1) : state.Revision
                };
                break;
            case StoragePrincipalOperation.Retire:
                if (existing is null)
                {
                    throw new InvalidOperationException("Principal does not exist.");
                }

                if (state.PrincipalDependencies.Any(predicate: d => d.Principal == change.Target && d.Kind != StoragePrincipalDependencyKind.Membership))
                {
                    throw new InvalidOperationException("Principal has live ownership or delegation dependencies.");
                }

                if (state.RetiredPrincipals.Length >= 1024)
                {
                    throw new InvalidOperationException("Retirement tombstone quota exceeded.");
                }

                next = state with
                {
                    Revision = checked(state.Revision + 1),
                    Principals = state.Principals.Where(predicate: p => p.Id != change.Target)
            .ToArray(),
                    RetiredPrincipals = [..state.RetiredPrincipals, change.Target],
                    Permissions = state.Permissions.Where(predicate: p => p.Principal != change.Target)
            .ToArray(),
                    DatabasePermissions = state.DatabasePermissions.Where(predicate: p => p.Principal != change.Target)
            .ToArray(),
                    PrincipalDependencies = state.PrincipalDependencies.Where(predicate: p => p.Principal != change.Target)
            .ToArray()
                };
                break;
            default:
                throw new ArgumentException("Unknown principal operation.");
        }

        return securityCatalogMutation with
        {
            State = next,
            Record = new sql_storage_engine.Models.Security.StorageAuditRecord
            {
                OperationId = change.OperationId,
                CorrelationId = change.CorrelationId,
                Principal = change.Actor,
                ObjectId = securityCatalogMutation.DatabaseId.Value,
                Action = StoragePermissionAction.ManagePrincipals,
                Kind = StorageAuditKind.SecurityChange,
                Revision = next.Revision,
                TargetPrincipal = change.Target,
                PrincipalOperation = change.Operation
            }
        };
    });

    private static void ValidatePrincipal(StoragePrincipalId principal)
    {
        if (principal.Issuer == Guid.Empty || principal.Subject == Guid.Empty)
        {
            throw new ArgumentException(message: "A principal needs nonempty issuer and subject identities.");
        }
    }

    private static StorageSecurityState Current(SecurityCatalogMutation securityCatalogMutation, long expected, Guid operation)
    {
        var state = securityCatalogMutation.State!;

        if (expected != state.Revision)
        {
            throw new StorageAuthorizationException(reason: "Security revision is stale.");
        }

        if (operation == Guid.Empty || state.Audit.Any(predicate: a => a.OperationId == operation))
        {
            throw new ArgumentException(message: "Invalid or previously recorded operation identity; resolve before retrying.");
        }

        return state;
    }
}