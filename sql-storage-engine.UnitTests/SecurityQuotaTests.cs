// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Security;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Models;
using sql_storage_engine.Exposures.Transactions;

namespace sql_storage_engine.UnitTests;
public sealed class SecurityQuotaTests
{
    [Test]
    public async Task PrincipalBoundaryDuplicateAndRevisionOverflowHaveNoUnrecordedChanges()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var principals = Enumerable.Range(0, 1023).Select(_ => new StoragePrincipal { Id = new sql_storage_engine.Models.Security.StoragePrincipalId { Issuer = Guid.NewGuid(), Subject = Guid.NewGuid() }, Active = true }).ToArray();
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync((_, t) => db.Engine.WriteSecurityAsync(sql_storage_engine.Exposures.Security.StorageSecurityStateExposure.CreateStorageSecurityState() with { Principals = principals }, t));
        var last = new StoragePrincipal
        {
            Id = new sql_storage_engine.Models.Security.StoragePrincipalId
            {
                Issuer = Guid.NewGuid(),
                Subject = Guid.NewGuid()
            },
            Active = true
        };
        await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().SetPrincipalAsync(last, 1, Guid.NewGuid(), t));
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            c.ReadSecurity().ReadSnapshot().Principals.Count.Should().Be(1024);
            await c.ReadSecurity().SetPrincipalAsync(last, 2, Guid.NewGuid(), t);
            c.ReadSecurity().ReadSnapshot().Revision.Should().Be(2);
        });
        Func<Task> over = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().SetPrincipalAsync(new sql_storage_engine.Models.Security.StoragePrincipal { Id = new sql_storage_engine.Models.Security.StoragePrincipalId { Issuer = Guid.NewGuid(), Subject = Guid.NewGuid() }, Active = true }, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t));
        await over.Should().ThrowAsync<InvalidOperationException>();
        await root.ExecuteStatementAsync((_, t) => db.Engine.WriteSecurityAsync(db.Engine.ReadStorageSecurityState() with { Revision = long.MaxValue }, t));
        Func<Task> overflow = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().SetPrincipalAsync(last with { Active = false }, long.MaxValue, Guid.NewGuid(), t));
        await overflow.Should().ThrowAsync<OverflowException>();
        await root.RollbackAsync();
        (await db.Engine.ReadSecurity().ReadAsync()).Principals.Should().BeEmpty();
    }

    [Test]
    public async Task GrantAndAuditQuotasRejectOneOverWithoutDroppingDenyOrAudit()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var principal = new StoragePrincipal
        {
            Id = new sql_storage_engine.Models.Security.StoragePrincipalId
            {
                Issuer = Guid.NewGuid(),
                Subject = Guid.NewGuid()
            },
            Active = true
        };
        var grants = Enumerable.Range(0, 1023).Select(_ => new StoragePermission { Principal = principal.Id, ObjectId = Guid.NewGuid(), Action = StoragePermissionAction.Select, Effect = StoragePermissionEffect.Deny }).ToArray();
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync((_, t) => db.Engine.WriteSecurityAsync(sql_storage_engine.Exposures.Security.StorageSecurityStateExposure.CreateStorageSecurityState() with { Revision = 2, Principals = [principal], Permissions = grants }, t));
        var grant = new StoragePermission
        {
            Principal = principal.Id,
            ObjectId = db.Table.ObjectId,
            Action = StoragePermissionAction.Update,
            Effect = StoragePermissionEffect.Grant
        };
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            await c.ReadSecurity().SetPermissionAsync(grant, false, 2, Guid.NewGuid(), t);
            c.ReadSecurity().ReadSnapshot().Permissions.Count.Should().Be(1024);
            await c.ReadSecurity().SetPermissionAsync(grant, false, 3, Guid.NewGuid(), t);
            c.ReadSecurity().ReadSnapshot().Revision.Should().Be(3);
        });
        Func<Task> over = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().SetPermissionAsync(grant with { Effect = StoragePermissionEffect.Deny }, false, 3, Guid.NewGuid(), t));
        await over.Should().ThrowAsync<InvalidOperationException>();
        var audit = Enumerable.Range(0, 4095).Select(_ => new sql_storage_engine.Models.Security.StorageAuditRecord{OperationId=Guid.NewGuid(),CorrelationId=Guid.NewGuid(),Principal=null,ObjectId=null,Action=StoragePermissionAction.ManagePermissions,Kind=StorageAuditKind.SecurityChange,Revision=3}).ToArray();
        await root.ExecuteStatementAsync((_, t) => db.Engine.WriteSecurityAsync(db.Engine.ReadStorageSecurityState() with { Audit = audit }, t));
        await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().SetPermissionAsync(grant, false, 3, Guid.NewGuid(), t));
        await over.Should().ThrowAsync<InvalidOperationException>();
        Func<Task> auditOver = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().SetPermissionAsync(grant, false, 3, Guid.NewGuid(), t));
        await auditOver.Should().ThrowAsync<InvalidOperationException>();
        await root.ExecuteStatementAsync((c, _) =>
        {
            c.ReadSecurity().ReadSnapshot().MutationAudit.Count.Should().Be(4096);
            return ValueTask.CompletedTask;
        });
        await root.RollbackAsync();
    }
}