// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Security;
/// <summary>Data representing a security catalog at one revision.</summary>
public sealed class StorageSecuritySnapshot
{
    public long Revision { get; init; }
    public IReadOnlyList<StoragePrincipalId> RetiredPrincipals { get => _retiredPrincipals!; init => _retiredPrincipals = value; }
    public IReadOnlyList<StorageDatabasePermission> DatabasePermissions { get => _databasePermissions!; init => _databasePermissions = value; }
    public IReadOnlyList<StoragePrincipalReference> PrincipalDependencies { get => _principalDependencies!; init => _principalDependencies = value; }
    public IReadOnlyList<StoragePrincipal> Principals { get => _principals!; init => _principals = value; }
    public IReadOnlyList<StoragePermission> Permissions { get => _permissions!; init => _permissions = value; }
    public IReadOnlyList<StorageAuditRecord> MutationAudit { get => _mutationAudit!; init => _mutationAudit = value; }

    private IReadOnlyList<StoragePrincipalId>? _retiredPrincipals;
    private IReadOnlyList<StorageDatabasePermission>? _databasePermissions;
    private IReadOnlyList<StoragePrincipalReference>? _principalDependencies;
    private IReadOnlyList<StoragePrincipal>? _principals;
    private IReadOnlyList<StoragePermission>? _permissions;
    private IReadOnlyList<StorageAuditRecord>? _mutationAudit;
}