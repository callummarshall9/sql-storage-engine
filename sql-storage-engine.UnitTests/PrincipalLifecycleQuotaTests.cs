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
public sealed class PrincipalLifecycleQuotaTests
{
    [Test]
    public async Task TombstoneExactBoundaryNeverEvictsAndOneOverRetainsPrincipal()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await PrincipalLifecycleTests.Bootstrap(db);
        var target = PrincipalLifecycleTests.Target();
        var next = PrincipalLifecycleTests.Target();
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            await db.Engine.WriteSecurityAsync(db.Engine.ReadStorageSecurityState() with { RetiredPrincipals = Enumerable.Range(0, 1023).Select(_ => PrincipalLifecycleTests.Target()).ToArray() }, t);
            await c.ReadSecurity().ChangePrincipalAsync(PrincipalLifecycleTests.Request(db, c, target, StoragePrincipalOperation.Create), t);
            await c.ReadSecurity().ChangePrincipalAsync(PrincipalLifecycleTests.Request(db, c, target, StoragePrincipalOperation.Retire), t);
            await c.ReadSecurity().ChangePrincipalAsync(PrincipalLifecycleTests.Request(db, c, next, StoragePrincipalOperation.Create), t);
        });
        Func<Task> over = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().ChangePrincipalAsync(PrincipalLifecycleTests.Request(db, c, next, StoragePrincipalOperation.Retire), t));
        await over.Should().ThrowAsync<InvalidOperationException>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await root.CommitAsync();
        watch.Stop();
        TestContext.Out.WriteLine($"1024-tombstone commit: {watch.Elapsed.TotalMilliseconds:F3} ms; {new FileInfo(db.Path).Length} file bytes");
        await db.ReopenAsync();
        var snapshot = await db.Engine.ReadSecurity().ReadAsync();
        snapshot.RetiredPrincipals.Count.Should().Be(1024);
        snapshot.RetiredPrincipals.Should().Contain(target);
        snapshot.Principals.Should().Contain(p => p.Id == next);
    }

    [TestCase("principals")]
    [TestCase("dependencies")]
    [TestCase("database-permissions")]
    public async Task ExactAndOneOverLimitsRejectWithoutPartialPublication(string kind)
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        await PrincipalLifecycleTests.Bootstrap(db);
        var principals = Enumerable.Range(0, 1023).Select(_ => new StoragePrincipal { Id = PrincipalLifecycleTests.Target(), Active = true }).ToArray();
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            var state = db.Engine.ReadStorageSecurityState();
            await db.Engine.WriteSecurityAsync(state with { Principals = kind == "principals" ? [state.Principals[0], ..principals.Take(1022)] : [state.Principals[0], ..principals], PrincipalDependencies = kind == "dependencies" ? Enumerable.Range(0, 1023).Select(_ => new StoragePrincipalReference { Principal = PrincipalLifecycleTests.Actor, Kind = StoragePrincipalDependencyKind.Membership, ReferenceId = Guid.NewGuid() }).ToArray() : [], DatabasePermissions = kind == "database-permissions" ? [..state.DatabasePermissions, ..principals.Take(1022).Select(p => new StorageDatabasePermission { Principal = p.Id, Action = StorageDatabasePermissionAction.ManagePrincipals, Effect = StoragePermissionEffect.Grant })] : state.DatabasePermissions }, t);
        });
        async ValueTask Add(IStorageTransactionContext c, CancellationToken t, bool last)
        {
            if (kind == "principals")
                await c.ReadSecurity().ChangePrincipalAsync(PrincipalLifecycleTests.Request(db, c, PrincipalLifecycleTests.Target(), StoragePrincipalOperation.Create), t);
            else if (kind == "dependencies")
                await c.ReadSecurity().SetPrincipalDependencyAsync(new sql_storage_engine.Models.Security.StoragePrincipalReference { Principal = PrincipalLifecycleTests.Actor, Kind = StoragePrincipalDependencyKind.Membership, ReferenceId = Guid.NewGuid() }, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
            else
                await c.ReadSecurity().SetDatabasePermissionAsync(new sql_storage_engine.Models.Security.StorageDatabasePermission { Principal = principals[^1].Id, Action = StorageDatabasePermissionAction.ManagePrincipals, Effect = last ? StoragePermissionEffect.Grant : StoragePermissionEffect.Deny }, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
        }

        await root.ExecuteStatementAsync((c, t) => Add(c, t, true));
        var before = db.Engine.ReadStorageSecurityState();
        Func<Task> over = async () => await root.ExecuteStatementAsync((c, t) => Add(c, t, false));
        await over.Should().ThrowAsync<InvalidOperationException>();
        db.Engine.ReadStorageSecurityState().Should().BeEquivalentTo(before);
        await root.CommitAsync();
        await db.ReopenAsync();
    }
}