using AwesomeAssertions;
using sql_storage_engine.Security;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class SecurityCatalogTests
{
    private static readonly StoragePrincipalId Principal = new(Guid.Parse("11111111-2222-3333-4444-555555555555"), Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
    private static async Task<StorageObjectBinding> Provision(ExplicitTransactionTestDatabase db, StoragePermissionAction action = StoragePermissionAction.Update)
    {
        StorageObjectBinding binding = null!;
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync(async (context, token) =>
        {
            await context.Security.SetPrincipalAsync(new(Principal, true), context.Security.Snapshot.Revision, Guid.NewGuid(), token);
            binding = context.Security.ResolveObject(db.Table.Id);
            await context.Security.SetPermissionAsync(new(Principal, binding.ObjectId, action, StoragePermissionEffect.Grant), false,
                context.Security.Snapshot.Revision, Guid.NewGuid(), token);
        });
        (await root.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
        return binding;
    }
    private static StorageAuthorizationRequest Request(StorageObjectBinding binding, long revision, Guid operation, StoragePermissionAction action = StoragePermissionAction.Update) =>
        new(Principal, revision, operation, operation, [new(binding, action)]);
    private static async ValueTask Change(IStorageTransactionContext context, ExplicitTransactionTestDatabase db, CancellationToken token)
    {
        var table = await context.OpenTableAsync(db.Table.Id, token);
        await table.UpdateAsync(db.First, new RowUpdate([new ColumnUpdate(1, SqlValue.Integer(25))]), token);
    }

    [Test]
    public async Task MutationAndAuditCommitTogetherAndBindingsSurviveRestart()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var binding = await Provision(db); var operation = Guid.NewGuid();
        await using (var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30))))
        {
            await root.ExecuteStatementAsync((context, token) => context.Security.ExecuteAuthorizedAsync(
                Request(binding, context.Security.Snapshot.Revision, operation), (c, t) => Change(c, db, t), token));
            (await root.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
        }
        await db.ReopenAsync();
        (await db.BalancesAsync()).Should().Equal(25, 100);
        var snapshot = await db.Engine.Security.ReadAsync();
        snapshot.Permissions.Should().ContainSingle(); snapshot.MutationAudit.Should().Contain(a => a.OperationId == operation);
        db.Engine.Catalog.Tables.Single(t => t.Id == db.Table.Id).ObjectId.Should().Be(binding.ObjectId);
        await using var retry = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        Func<Task> duplicate = async () => await retry.ExecuteStatementAsync((context, token) => context.Security.ExecuteAuthorizedAsync(
            Request(binding, snapshot.Revision, operation), (_, _) => throw new Exception("Must not execute"), token));
        await duplicate.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task SavepointRollbackRemovesMutationAuditButRetainsReadAdmission()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var binding = await Provision(db); var write = Guid.NewGuid(); var read = Guid.NewGuid();
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync((c, t) => c.Security.SetPermissionAsync(new(Principal, binding.ObjectId,
            StoragePermissionAction.Select, StoragePermissionEffect.Grant), false, c.Security.Snapshot.Revision, Guid.NewGuid(), t));
        var point = await root.CreateSavepointAsync("audit");
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            await c.Security.ExecuteAuthorizedAsync(Request(binding, c.Security.Snapshot.Revision, write), (x, y) => Change(x, db, y), t);
            await c.Security.ExecuteAuthorizedAsync(Request(binding, c.Security.Snapshot.Revision, read, StoragePermissionAction.Select), (_, _) => ValueTask.CompletedTask, t);
        });
        await root.RollbackToSavepointAsync(point);
        (await root.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
        await db.ReopenAsync();
        (await db.BalancesAsync()).Should().Equal(100, 100);
        (await db.Engine.Security.ReadAsync()).MutationAudit.Should().NotContain(a => a.OperationId == write);
        (await db.Engine.Security.ReadEventsAsync()).Should().ContainSingle(a => a.OperationId == read);
    }

    [Test]
    public async Task SwallowedAuditFailureStillRollsBackDataAndAudit()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var binding = await Provision(db); var operation = Guid.NewGuid();
        db.Engine.TransactionObserver = stage => { if (stage == "security-mutation-before-audit") throw new IOException("fault"); };
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        Func<Task> failed = async () => await root.ExecuteStatementAsync(async (c, t) =>
        {
            try { await c.Security.ExecuteAuthorizedAsync(Request(binding, c.Security.Snapshot.Revision, operation), (x, y) => Change(x, db, y), t); }
            catch (IOException) { }
        });
        await failed.Should().ThrowAsync<InvalidOperationException>();
        db.Engine.TransactionObserver = null;
        await root.RollbackAsync();
        (await db.BalancesAsync()).Should().Equal(100, 100);
        (await db.Engine.Security.ReadAsync()).MutationAudit.Should().NotContain(a => a.OperationId == operation);
    }

    [Test]
    public async Task RevocationWaitsForRootAndOldRevisionFailsAfterRegrant()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var binding = await Provision(db); var revision = (await db.Engine.Security.ReadAsync()).Revision;
        await using var reader = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await reader.ExecuteStatementAsync((c, t) => c.Security.ExecuteAuthorizedAsync(Request(binding, revision, Guid.NewGuid()), (_, _) => ValueTask.CompletedTask, t));
        var waiting = db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30))).AsTask();
        waiting.IsCompleted.Should().BeFalse();
        await reader.CommitAsync();
        await using var revoke = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        await revoke.ExecuteStatementAsync(async (c, t) =>
        {
            var grant = new StoragePermission(Principal, binding.ObjectId, StoragePermissionAction.Update, StoragePermissionEffect.Grant);
            await c.Security.SetPermissionAsync(grant, true, c.Security.Snapshot.Revision, Guid.NewGuid(), t);
            await c.Security.SetPermissionAsync(grant, false, c.Security.Snapshot.Revision, Guid.NewGuid(), t);
        });
        await revoke.CommitAsync();
        await using var stale = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        Func<Task> denied = async () => await stale.ExecuteStatementAsync((c, t) => c.Security.ExecuteAuthorizedAsync(
            Request(binding, revision, Guid.NewGuid()), (_, _) => throw new Exception("Must not execute"), t));
        await denied.Should().ThrowAsync<StorageAuthorizationException>();
    }

    [Test]
    public async Task DenyOverridesGrantAndEscapedContextIsRejected()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); var binding = await Provision(db);
        IStorageSecurityContext escaped = null!;
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            escaped = c.Security;
            await c.Security.SetPermissionAsync(new(Principal, binding.ObjectId, StoragePermissionAction.Update, StoragePermissionEffect.Deny),
                false, c.Security.Snapshot.Revision, Guid.NewGuid(), t);
        });
        Action outside = () => _ = escaped.Snapshot; outside.Should().Throw<InvalidOperationException>();
        Func<Task> denied = async () => await root.ExecuteStatementAsync((c, t) => c.Security.ExecuteAuthorizedAsync(
            Request(binding, c.Security.Snapshot.Revision, Guid.NewGuid()), (_, _) => throw new Exception("Must not execute"), t));
        await denied.Should().ThrowAsync<StorageAuthorizationException>();
    }

    [Test]
    public async Task CancellationAfterDataChangeAbortsWholeRoot()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); var binding = await Provision(db); var operation = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        Func<Task> canceled = async () => await root.ExecuteStatementAsync((c, t) => c.Security.ExecuteAuthorizedAsync(
            Request(binding, c.Security.Snapshot.Revision, operation), async (x, y) => { await Change(x, db, y); cancellation.Cancel(); }, t), cancellation.Token);
        await canceled.Should().ThrowAsync<OperationCanceledException>();
        root.State.Should().Be(StorageTransactionState.Aborted);
        (await db.BalancesAsync()).Should().Equal(100, 100);
        (await db.Engine.Security.ReadAsync()).MutationAudit.Should().NotContain(a => a.OperationId == operation);
    }

    [Test]
    public async Task AccessLimitAndForeignOrStaleBindingsRejectBeforeEffects()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); var binding = await Provision(db);
        await using var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync((c, t) => c.Security.ExecuteAuthorizedAsync(new(Principal, c.Security.Snapshot.Revision,
            Guid.NewGuid(), Guid.NewGuid(), Enumerable.Repeat(new StorageRequiredAccess(binding, StoragePermissionAction.Update), 256)),
            (_, _) => ValueTask.CompletedTask, t));
        Func<Task> over = async () => await root.ExecuteStatementAsync((c, t) => c.Security.ExecuteAuthorizedAsync(new(Principal,
            c.Security.Snapshot.Revision, Guid.NewGuid(), Guid.NewGuid(), Enumerable.Repeat(new StorageRequiredAccess(binding, StoragePermissionAction.Update), 257)),
            (_, _) => throw new Exception("Must not execute"), t));
        await over.Should().ThrowAsync<ArgumentException>();
        foreach (var invalid in new[] { binding with { ObjectId = Guid.NewGuid() }, binding with { SchemaVersion = binding.SchemaVersion + 1 },
            binding with { DatabaseId = sql_storage_engine.Identifiers.DatabaseId.New() } })
        {
            Func<Task> stale = async () => await root.ExecuteStatementAsync((c, t) => c.Security.ExecuteAuthorizedAsync(
                Request(invalid, c.Security.Snapshot.Revision, Guid.NewGuid()), (_, _) => throw new Exception("Must not execute"), t));
            await stale.Should().ThrowAsync<StorageAuthorizationException>();
        }
    }

    [Test]
    public async Task ReadAuditFailurePreventsCallbackAndRootDisposalRollsBackPrincipal()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); var binding = await Provision(db, StoragePermissionAction.Select);
        db.Engine.TransactionObserver = stage => { if (stage == "security-event-before-write") throw new IOException(); };
        await using (var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30))))
        {
            Func<Task> failed = async () => await root.ExecuteStatementAsync((c, t) => c.Security.ExecuteAuthorizedAsync(
                Request(binding, c.Security.Snapshot.Revision, Guid.NewGuid(), StoragePermissionAction.Select), (_, _) => throw new Exception("Must not execute"), t));
            await failed.Should().ThrowAsync<StorageAuditException>();
            await root.ExecuteStatementAsync((c, t) => c.Security.SetPrincipalAsync(new(Principal, false), c.Security.Snapshot.Revision, Guid.NewGuid(), t));
        }
        db.Engine.TransactionObserver = null;
        (await db.Engine.Security.ReadAsync()).Principals.Single().Active.Should().BeTrue();
        (await db.Engine.Security.ReadEventsAsync()).Should().BeEmpty();
    }
}
