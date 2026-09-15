using AwesomeAssertions;
using sql_storage_engine.Security;

namespace sql_storage_engine.UnitTests;

public sealed class SecurityQuotaTests
{
    [Test]
    public async Task PrincipalBoundaryDuplicateAndRevisionOverflowHaveNoUnrecordedChanges()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var principals = Enumerable.Range(0, 1023).Select(_ => new StoragePrincipal(new(Guid.NewGuid(), Guid.NewGuid()), true)).ToArray();
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync((_, t) => db.Engine.SecurityCatalog.WriteSecurityAsync(new() { Principals = principals }, t));
        var last = new StoragePrincipal(new(Guid.NewGuid(), Guid.NewGuid()), true);
        await root.ExecuteStatementAsync((c, t) => c.Security.SetPrincipalAsync(last, 1, Guid.NewGuid(), t));
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            c.Security.Snapshot.Principals.Count.Should().Be(1024);
            await c.Security.SetPrincipalAsync(last, 2, Guid.NewGuid(), t);
            c.Security.Snapshot.Revision.Should().Be(2);
        });
        Func<Task> over = async () => await root.ExecuteStatementAsync((c, t) => c.Security.SetPrincipalAsync(
            new(new(Guid.NewGuid(), Guid.NewGuid()), true), c.Security.Snapshot.Revision, Guid.NewGuid(), t));
        await over.Should().ThrowAsync<InvalidOperationException>();
        await root.ExecuteStatementAsync((_, t) => db.Engine.SecurityCatalog.WriteSecurityAsync(db.Engine.SecurityCatalog.SecurityState with { Revision = long.MaxValue }, t));
        Func<Task> overflow = async () => await root.ExecuteStatementAsync((c, t) => c.Security.SetPrincipalAsync(last with { Active = false }, long.MaxValue, Guid.NewGuid(), t));
        await overflow.Should().ThrowAsync<OverflowException>();
        await root.RollbackAsync();
        (await db.Engine.Security.ReadAsync()).Principals.Should().BeEmpty();
    }

    [Test]
    public async Task GrantAndAuditQuotasRejectOneOverWithoutDroppingDenyOrAudit()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var principal = new StoragePrincipal(new(Guid.NewGuid(), Guid.NewGuid()), true);
        var grants = Enumerable.Range(0, 1023).Select(_ => new StoragePermission(principal.Id, Guid.NewGuid(), StoragePermissionAction.Select, StoragePermissionEffect.Deny)).ToArray();
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync((_, t) => db.Engine.SecurityCatalog.WriteSecurityAsync(new() { Revision = 2, Principals = [principal], Permissions = grants }, t));
        var grant = new StoragePermission(principal.Id, db.Table.ObjectId, StoragePermissionAction.Update, StoragePermissionEffect.Grant);
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            await c.Security.SetPermissionAsync(grant, false, 2, Guid.NewGuid(), t);
            c.Security.Snapshot.Permissions.Count.Should().Be(1024);
            await c.Security.SetPermissionAsync(grant, false, 3, Guid.NewGuid(), t);
            c.Security.Snapshot.Revision.Should().Be(3);
        });
        Func<Task> over = async () => await root.ExecuteStatementAsync((c, t) => c.Security.SetPermissionAsync(grant with { Effect = StoragePermissionEffect.Deny }, false, 3, Guid.NewGuid(), t));
        await over.Should().ThrowAsync<InvalidOperationException>();
        var audit = Enumerable.Range(0, 4095).Select(_ => new StorageAuditRecord(Guid.NewGuid(), Guid.NewGuid(), null, null,
            StoragePermissionAction.ManagePermissions, StorageAuditKind.SecurityChange, 3)).ToArray();
        await root.ExecuteStatementAsync((_, t) => db.Engine.SecurityCatalog.WriteSecurityAsync(db.Engine.SecurityCatalog.SecurityState with { Audit = audit }, t));
        await root.ExecuteStatementAsync((c, t) => c.Security.SetPermissionAsync(grant, false, 3, Guid.NewGuid(), t));
        await over.Should().ThrowAsync<InvalidOperationException>();
        Func<Task> auditOver = async () => await root.ExecuteStatementAsync((c, t) => c.Security.SetPermissionAsync(grant, false, 3, Guid.NewGuid(), t));
        await auditOver.Should().ThrowAsync<InvalidOperationException>();
        await root.ExecuteStatementAsync((c, _) => { c.Security.Snapshot.MutationAudit.Count.Should().Be(4096); return ValueTask.CompletedTask; });
        await root.RollbackAsync();
    }
}
