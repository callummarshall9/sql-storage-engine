using AwesomeAssertions;
using sql_storage_engine.Security;

namespace sql_storage_engine.UnitTests;

public sealed class PrincipalLifecycleTests
{
    internal static readonly StoragePrincipalId Actor = new(Guid.NewGuid(), Guid.NewGuid());
    internal static StoragePrincipalId Target() => new(Guid.NewGuid(), Guid.NewGuid());
    internal static async Task Bootstrap(ExplicitTransactionTestDatabase db)
    {
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            await c.Security.SetPrincipalAsync(new(Actor, true), c.Security.Snapshot.Revision, Guid.NewGuid(), t);
            await c.Security.SetDatabasePermissionAsync(new(Actor, StorageDatabasePermissionAction.ManagePrincipals, StoragePermissionEffect.Grant), false, c.Security.Snapshot.Revision, Guid.NewGuid(), t);
        });
        await root.CommitAsync();
    }
    internal static StoragePrincipalChange Request(ExplicitTransactionTestDatabase db, IStorageTransactionContext c, StoragePrincipalId target, StoragePrincipalOperation operation)
        => new(db.Engine.DatabaseId, Actor, target, operation, c.Security.Snapshot.Revision, Guid.NewGuid(), Guid.NewGuid());

    [Test]
    public async Task RetirementRemovesAuthorityAndMembershipAndPermanentlyPreventsReuse()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); await Bootstrap(db);
        var target = Target(); var operation = Guid.NewGuid();
        await using (var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30))))
        {
            await root.ExecuteStatementAsync(async (c, t) =>
            {
                await c.Security.ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Create), t);
                await c.Security.SetPermissionAsync(new(target, db.Table.ObjectId, StoragePermissionAction.Select, StoragePermissionEffect.Grant), false, c.Security.Snapshot.Revision, Guid.NewGuid(), t);
                await c.Security.SetDatabasePermissionAsync(new(target, StorageDatabasePermissionAction.ManagePrincipals, StoragePermissionEffect.Grant), false, c.Security.Snapshot.Revision, Guid.NewGuid(), t);
                await c.Security.SetPrincipalDependencyAsync(new(target, StoragePrincipalDependencyKind.Membership, Guid.NewGuid()), false, c.Security.Snapshot.Revision, Guid.NewGuid(), t);
                await c.Security.ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Deactivate), t);
                var revision = c.Security.Snapshot.Revision;
                await c.Security.ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Deactivate), t);
                c.Security.Snapshot.Revision.Should().Be(revision);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                await c.Security.ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Retire) with { OperationId = operation }, t);
                watch.Stop(); TestContext.Out.WriteLine($"Principal retirement callback: {watch.Elapsed.TotalMilliseconds:F3} ms");
            });
            await root.CommitAsync();
        }
        await db.ReopenAsync(); var snapshot = await db.Engine.Security.ReadAsync();
        snapshot.RetiredPrincipals.Should().ContainSingle().Which.Should().Be(target);
        snapshot.Principals.Should().NotContain(p => p.Id == target);
        snapshot.Permissions.Should().BeEmpty(); snapshot.PrincipalDependencies.Should().BeEmpty();
        snapshot.DatabasePermissions.Should().NotContain(p => p.Principal == target);
        var audit = snapshot.MutationAudit.Single(a => a.OperationId == operation);
        audit.Principal.Should().Be(Actor); audit.TargetPrincipal.Should().Be(target); audit.PrincipalOperation.Should().Be(StoragePrincipalOperation.Retire);
        await using var retry = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        Func<Task> create = async () => await retry.ExecuteStatementAsync((c, t) => c.Security.ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Create), t));
        await create.Should().ThrowAsync<InvalidOperationException>();
        Func<Task> legacy = async () => await retry.ExecuteStatementAsync((c, t) => c.Security.SetPrincipalAsync(new(target, true), c.Security.Snapshot.Revision, Guid.NewGuid(), t));
        await legacy.Should().ThrowAsync<InvalidOperationException>();
        await retry.ExecuteStatementAsync((c, t) => c.Security.ChangePrincipalAsync(Request(db, c, Target(), StoragePrincipalOperation.Create), t));
        await retry.CommitAsync();
    }

    [TestCase(StoragePrincipalDependencyKind.Ownership)]
    [TestCase(StoragePrincipalDependencyKind.Delegation)]
    public async Task LiveDependenciesBlockRetirementUntilExplicitlyRemoved(StoragePrincipalDependencyKind kind)
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); await Bootstrap(db); var target = Target(); var link = new StoragePrincipalDependency(target, kind, Guid.NewGuid());
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync(async (c, t) => { await c.Security.ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Create), t); await c.Security.SetPrincipalDependencyAsync(link, false, c.Security.Snapshot.Revision, Guid.NewGuid(), t); });
        Func<Task> retire = async () => await root.ExecuteStatementAsync((c, t) => c.Security.ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Retire), t));
        await retire.Should().ThrowAsync<InvalidOperationException>();
        await root.ExecuteStatementAsync(async (c, t) => { await c.Security.SetPrincipalDependencyAsync(link, true, c.Security.Snapshot.Revision, Guid.NewGuid(), t); await c.Security.ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Retire), t); });
        await root.CommitAsync(); await db.ReopenAsync(); (await db.Engine.Security.ReadAsync()).RetiredPrincipals.Should().Contain(target);
    }

    [TestCase("object-only")]
    [TestCase("deny")]
    [TestCase("inactive")]
    [TestCase("stale")]
    [TestCase("foreign")]
    [TestCase("duplicate")]
    [TestCase("missing")]
    [TestCase("replayed")]
    [TestCase("invalid-operation")]
    public async Task InvalidOrUnauthorizedRequestsHaveNoLifecycleEffects(string mode)
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); await Bootstrap(db); var target = Target();
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            if (mode == "object-only") { await c.Security.SetDatabasePermissionAsync(new(Actor, StorageDatabasePermissionAction.ManagePrincipals, StoragePermissionEffect.Grant), true, c.Security.Snapshot.Revision, Guid.NewGuid(), t); await c.Security.SetPermissionAsync(new(Actor, db.Table.ObjectId, StoragePermissionAction.ManagePermissions, StoragePermissionEffect.Grant), false, c.Security.Snapshot.Revision, Guid.NewGuid(), t); }
            if (mode == "deny") await c.Security.SetDatabasePermissionAsync(new(Actor, StorageDatabasePermissionAction.ManagePrincipals, StoragePermissionEffect.Deny), false, c.Security.Snapshot.Revision, Guid.NewGuid(), t);
            if (mode == "inactive") await c.Security.SetPrincipalAsync(new(Actor, false), c.Security.Snapshot.Revision, Guid.NewGuid(), t);
        });
        var before = db.Engine.SecurityCatalog.SecurityState;
        Func<Task> invalid = async () => await root.ExecuteStatementAsync((c, t) =>
        {
            var request = Request(db, c, target, StoragePrincipalOperation.Create);
            request = mode switch { "stale" => request with { Revision = 1 }, "foreign" => request with { DatabaseId = new(Guid.NewGuid()) }, "duplicate" => request with { Target = Actor }, "missing" => request with { Operation = StoragePrincipalOperation.Retire }, "replayed" => request with { OperationId = before.Audit[0].OperationId }, "invalid-operation" => request with { Operation = (StoragePrincipalOperation)99 }, _ => request };
            return c.Security.ChangePrincipalAsync(request, t);
        });
        await invalid.Should().ThrowAsync<Exception>();
        db.Engine.SecurityCatalog.SecurityState.Should().BeEquivalentTo(before);
    }

    [Test]
    public async Task DatabaseAuthorityCannotEnterObjectPermissionOrAdmission()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); await Bootstrap(db);
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        Func<Task> invalid = async () => await root.ExecuteStatementAsync((c, t) => c.Security.SetPermissionAsync(new(Actor, db.Table.ObjectId, StoragePermissionAction.ManagePrincipals, StoragePermissionEffect.Grant), false, c.Security.Snapshot.Revision, Guid.NewGuid(), t));
        await invalid.Should().ThrowAsync<ArgumentException>();
        Func<Task> admission = async () => await root.ExecuteStatementAsync((c, t) => c.Security.ExecuteAuthorizedAsync(new(Actor, c.Security.Snapshot.Revision, Guid.NewGuid(), Guid.NewGuid(), [new(c.Security.ResolveObject(db.Table.Id), StoragePermissionAction.ManagePrincipals)]), (_, _) => throw new Exception("Must not execute"), t));
        await admission.Should().ThrowAsync<ArgumentException>();
    }
}
