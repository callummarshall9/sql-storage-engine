// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Security;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Models;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Exposures.Security;

namespace sql_storage_engine.UnitTests;
public sealed class PrincipalLifecycleTests
{
    internal static readonly StoragePrincipalId Actor = new sql_storage_engine.Models.Security.StoragePrincipalId
    {
        Issuer = Guid.NewGuid(),
        Subject = Guid.NewGuid()
    };
    internal static StoragePrincipalId Target() => new sql_storage_engine.Models.Security.StoragePrincipalId
    {
        Issuer = Guid.NewGuid(),
        Subject = Guid.NewGuid()
    };
    internal static async Task Bootstrap(ExplicitTransactionTestDatabase db)
    {
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            await c.ReadSecurity().SetPrincipalAsync(new sql_storage_engine.Models.Security.StoragePrincipal { Id = Actor, Active = true }, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
            await c.ReadSecurity().SetDatabasePermissionAsync(new sql_storage_engine.Models.Security.StorageDatabasePermission { Principal = Actor, Action = StorageDatabasePermissionAction.ManagePrincipals, Effect = StoragePermissionEffect.Grant }, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
        });
        await root.CommitAsync();
    }

    internal static StoragePrincipalChange Request(ExplicitTransactionTestDatabase db, IStorageTransactionContext c, StoragePrincipalId target, StoragePrincipalOperation operation) => new sql_storage_engine.Models.Security.StoragePrincipalChange
    {
        DatabaseId = db.Engine.ReadDatabaseId(),
        Actor = Actor,
        Target = target,
        Operation = operation,
        Revision = c.ReadSecurity().ReadSnapshot().Revision,
        OperationId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid()
    };
    [Test]
    public async Task RetirementRemovesAuthorityAndMembershipAndPermanentlyPreventsReuse()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await Bootstrap(db);
        var target = Target();
        var operation = Guid.NewGuid();
        await using (var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30))))
        {
            await root.ExecuteStatementAsync(async (c, t) =>
            {
                await c.ReadSecurity().ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Create), t);
                await c.ReadSecurity().SetPermissionAsync(new sql_storage_engine.Models.Security.StoragePermission { Principal = target, ObjectId = db.Table.ObjectId, Action = StoragePermissionAction.Select, Effect = StoragePermissionEffect.Grant }, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
                await c.ReadSecurity().SetDatabasePermissionAsync(new sql_storage_engine.Models.Security.StorageDatabasePermission { Principal = target, Action = StorageDatabasePermissionAction.ManagePrincipals, Effect = StoragePermissionEffect.Grant }, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
                await c.ReadSecurity().SetPrincipalDependencyAsync(new sql_storage_engine.Models.Security.StoragePrincipalReference { Principal = target, Kind = StoragePrincipalDependencyKind.Membership, ReferenceId = Guid.NewGuid() }, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
                await c.ReadSecurity().ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Deactivate), t);
                var revision = c.ReadSecurity().ReadSnapshot().Revision;
                await c.ReadSecurity().ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Deactivate), t);
                c.ReadSecurity().ReadSnapshot().Revision.Should().Be(revision);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                await c.ReadSecurity().ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Retire)with { OperationId = operation }, t);
                watch.Stop();
                TestContext.Out.WriteLine($"Principal retirement callback: {watch.Elapsed.TotalMilliseconds:F3} ms");
            });
            await root.CommitAsync();
        }

        await db.ReopenAsync();
        var snapshot = await db.Engine.ReadSecurity().ReadAsync();
        snapshot.RetiredPrincipals.Should().ContainSingle().Which.Should().Be(target);
        snapshot.Principals.Should().NotContain(p => p.Id == target);
        snapshot.Permissions.Should().BeEmpty();
        snapshot.PrincipalDependencies.Should().BeEmpty();
        snapshot.DatabasePermissions.Should().NotContain(p => p.Principal == target);
        var audit = snapshot.MutationAudit.Single(a => a.OperationId == operation);
        audit.Principal.Should().Be(Actor);
        audit.TargetPrincipal.Should().Be(target);
        audit.PrincipalOperation.Should().Be(StoragePrincipalOperation.Retire);
        await using var retry = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        Func<Task> create = async () => await retry.ExecuteStatementAsync((c, t) => c.ReadSecurity().ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Create), t));
        await create.Should().ThrowAsync<InvalidOperationException>();
        Func<Task> legacy = async () => await retry.ExecuteStatementAsync((c, t) => c.ReadSecurity().SetPrincipalAsync(new sql_storage_engine.Models.Security.StoragePrincipal { Id = target, Active = true }, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t));
        await legacy.Should().ThrowAsync<InvalidOperationException>();
        await retry.ExecuteStatementAsync((c, t) => c.ReadSecurity().ChangePrincipalAsync(Request(db, c, Target(), StoragePrincipalOperation.Create), t));
        await retry.CommitAsync();
    }

    [TestCase(StoragePrincipalDependencyKind.Ownership)]
    [TestCase(StoragePrincipalDependencyKind.Delegation)]
    public async Task LiveDependenciesBlockRetirementUntilExplicitlyRemoved(StoragePrincipalDependencyKind kind)
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await Bootstrap(db);
        var target = Target();
        var link = new StoragePrincipalReference
        {
            Principal = target,
            Kind = kind,
            ReferenceId = Guid.NewGuid()
        };
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            await c.ReadSecurity().ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Create), t);
            await c.ReadSecurity().SetPrincipalDependencyAsync(link, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
        });
        Func<Task> retire = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Retire), t));
        await retire.Should().ThrowAsync<InvalidOperationException>();
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            await c.ReadSecurity().SetPrincipalDependencyAsync(link, true, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
            await c.ReadSecurity().ChangePrincipalAsync(Request(db, c, target, StoragePrincipalOperation.Retire), t);
        });
        await root.CommitAsync();
        await db.ReopenAsync();
        (await db.Engine.ReadSecurity().ReadAsync()).RetiredPrincipals.Should().Contain(target);
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
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await Bootstrap(db);
        var target = Target();
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            if (mode == "object-only")
            {
                await c.ReadSecurity().SetDatabasePermissionAsync(new sql_storage_engine.Models.Security.StorageDatabasePermission { Principal = Actor, Action = StorageDatabasePermissionAction.ManagePrincipals, Effect = StoragePermissionEffect.Grant }, true, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
                await c.ReadSecurity().SetPermissionAsync(new sql_storage_engine.Models.Security.StoragePermission { Principal = Actor, ObjectId = db.Table.ObjectId, Action = StoragePermissionAction.ManagePermissions, Effect = StoragePermissionEffect.Grant }, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
            }

            if (mode == "deny")
                await c.ReadSecurity().SetDatabasePermissionAsync(new sql_storage_engine.Models.Security.StorageDatabasePermission { Principal = Actor, Action = StorageDatabasePermissionAction.ManagePrincipals, Effect = StoragePermissionEffect.Deny }, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
            if (mode == "inactive")
                await c.ReadSecurity().SetPrincipalAsync(new sql_storage_engine.Models.Security.StoragePrincipal { Id = Actor, Active = false }, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
        });
        var before = db.Engine.ReadStorageSecurityState();
        Func<Task> invalid = async () => await root.ExecuteStatementAsync((c, t) =>
        {
            var request = Request(db, c, target, StoragePrincipalOperation.Create);
            request = mode switch
            {
                "stale" => request with
                {
                    Revision = 1
                },
                "foreign" => request with
                {
                    DatabaseId = new sql_storage_engine.Models.Identifiers.DatabaseId
                    {
                        Value = Guid.NewGuid()
                    }
                },
                "duplicate" => request with
                {
                    Target = Actor
                },
                "missing" => request with
                {
                    Operation = StoragePrincipalOperation.Retire
                },
                "replayed" => request with
                {
                    OperationId = before.Audit[0].OperationId
                },
                "invalid-operation" => request with
                {
                    Operation = (StoragePrincipalOperation)99
                },
                _ => request
            };
            return c.ReadSecurity().ChangePrincipalAsync(request, t);
        });
        await invalid.Should().ThrowAsync<Exception>();
        db.Engine.ReadStorageSecurityState().Should().BeEquivalentTo(before);
    }

    [Test]
    public async Task DatabaseAuthorityCannotEnterObjectPermissionOrAdmission()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await Bootstrap(db);
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        Func<Task> invalid = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().SetPermissionAsync(new sql_storage_engine.Models.Security.StoragePermission { Principal = Actor, ObjectId = db.Table.ObjectId, Action = StoragePermissionAction.ManagePrincipals, Effect = StoragePermissionEffect.Grant }, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t));
        await invalid.Should().ThrowAsync<ArgumentException>();
        Func<Task> admission = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().ExecuteAuthorizedAsync(StorageAuthorizationRequestExposure.CreateStorageAuthorizationRequest(Actor, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), Guid.NewGuid(), [new sql_storage_engine.Models.Security.StorageRequiredAccess { Object = c.ReadSecurity().ResolveObject(db.Table.Id), Action = StoragePermissionAction.ManagePrincipals }]), (_, _) => throw new Exception("Must not execute"), t));
        await admission.Should().ThrowAsync<ArgumentException>();
    }
}