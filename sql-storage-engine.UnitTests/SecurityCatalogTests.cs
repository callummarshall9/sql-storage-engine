// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Security;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Models;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Exposures.Security;
using sql_storage_engine.Exposures.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class SecurityCatalogTests
{
    private static readonly StoragePrincipalId Principal = new sql_storage_engine.Models.Security.StoragePrincipalId
    {
        Issuer = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        Subject = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
    };
    private static async Task<StorageObjectBinding> Provision(ExplicitTransactionTestDatabase db, StoragePermissionAction action = StoragePermissionAction.Update)
    {
        StorageObjectBinding binding = null!;
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync(async (context, token) =>
        {
            await context.ReadSecurity().SetPrincipalAsync(new sql_storage_engine.Models.Security.StoragePrincipal { Id = Principal, Active = true }, context.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), token);
            binding = context.ReadSecurity().ResolveObject(db.Table.Id);
            await context.ReadSecurity().SetPermissionAsync(new sql_storage_engine.Models.Security.StoragePermission { Principal = Principal, ObjectId = binding.ObjectId, Action = action, Effect = StoragePermissionEffect.Grant }, false, context.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), token);
        });
        (await root.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
        return binding;
    }

    private static StorageAuthorizationRequest Request(StorageObjectBinding binding, long revision, Guid operation, StoragePermissionAction action = StoragePermissionAction.Update) => StorageAuthorizationRequestExposure.CreateStorageAuthorizationRequest(Principal, revision, operation, operation, [new sql_storage_engine.Models.Security.StorageRequiredAccess { Object = binding, Action = action }]);
    private static async ValueTask Change(IStorageTransactionContext context, ExplicitTransactionTestDatabase db, CancellationToken token)
    {
        var table = await context.OpenTableAsync(db.Table.Id, token);
        await table.UpdateAsync(db.First, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(25) }]), token);
    }

    [Test]
    public async Task MutationAndAuditCommitTogetherAndBindingsSurviveRestart()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var binding = await Provision(db);
        var operation = Guid.NewGuid();
        await using (var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30))))
        {
            await root.ExecuteStatementAsync((context, token) => context.ReadSecurity().ExecuteAuthorizedAsync(Request(binding, context.ReadSecurity().ReadSnapshot().Revision, operation), (c, t) => Change(c, db, t), token));
            (await root.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
        }

        await db.ReopenAsync();
        (await db.BalancesAsync()).Should().Equal(25, 100);
        var snapshot = await db.Engine.ReadSecurity().ReadAsync();
        snapshot.Permissions.Should().ContainSingle();
        snapshot.MutationAudit.Should().Contain(a => a.OperationId == operation);
        db.Engine.ReadCatalog().ReadTables().Single(t => t.Id == db.Table.Id).ObjectId.Should().Be(binding.ObjectId);
        await using var retry = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        Func<Task> duplicate = async () => await retry.ExecuteStatementAsync((context, token) => context.ReadSecurity().ExecuteAuthorizedAsync(Request(binding, snapshot.Revision, operation), (_, _) => throw new Exception("Must not execute"), token));
        await duplicate.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task SavepointRollbackRemovesMutationAuditButRetainsReadAdmission()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var binding = await Provision(db);
        var write = Guid.NewGuid();
        var read = Guid.NewGuid();
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().SetPermissionAsync(new sql_storage_engine.Models.Security.StoragePermission { Principal = Principal, ObjectId = binding.ObjectId, Action = StoragePermissionAction.Select, Effect = StoragePermissionEffect.Grant }, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t));
        var point = await root.CreateStorageSavepointAsync("audit");
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            await c.ReadSecurity().ExecuteAuthorizedAsync(Request(binding, c.ReadSecurity().ReadSnapshot().Revision, write), (x, y) => Change(x, db, y), t);
            await c.ReadSecurity().ExecuteAuthorizedAsync(Request(binding, c.ReadSecurity().ReadSnapshot().Revision, read, StoragePermissionAction.Select), (_, _) => ValueTask.CompletedTask, t);
        });
        await root.RollbackToSavepointAsync(point);
        (await root.CommitAsync()).State.Should().Be(StorageTransactionState.Committed);
        await db.ReopenAsync();
        (await db.BalancesAsync()).Should().Equal(100, 100);
        (await db.Engine.ReadSecurity().ReadAsync()).MutationAudit.Should().NotContain(a => a.OperationId == write);
        (await db.Engine.ReadSecurity().ReadEventsAsync()).Should().ContainSingle(a => a.OperationId == read);
    }

    [Test]
    public async Task SwallowedAuditFailureStillRollsBackDataAndAudit()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var binding = await Provision(db);
        var operation = Guid.NewGuid();
        db.Engine.WriteTransactionObserver(observer: stage =>
        {
            if (stage == "security-mutation-before-audit")
                throw new IOException("fault");
        });
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        Func<Task> failed = async () => await root.ExecuteStatementAsync(async (c, t) =>
        {
            try
            {
                await c.ReadSecurity().ExecuteAuthorizedAsync(Request(binding, c.ReadSecurity().ReadSnapshot().Revision, operation), (x, y) => Change(x, db, y), t);
            }
            catch (IOException)
            {
            }
        });
        await failed.Should().ThrowAsync<InvalidOperationException>();
        db.Engine.WriteTransactionObserver(observer: null);
        await root.RollbackAsync();
        (await db.BalancesAsync()).Should().Equal(100, 100);
        (await db.Engine.ReadSecurity().ReadAsync()).MutationAudit.Should().NotContain(a => a.OperationId == operation);
    }

    [Test]
    public async Task RevocationWaitsForRootAndOldRevisionFailsAfterRegrant()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var binding = await Provision(db);
        var revision = (await db.Engine.ReadSecurity().ReadAsync()).Revision;
        await using var reader = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await reader.ExecuteStatementAsync((c, t) => c.ReadSecurity().ExecuteAuthorizedAsync(Request(binding, revision, Guid.NewGuid()), (_, _) => ValueTask.CompletedTask, t));
        var waiting = db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30))).AsTask();
        waiting.IsCompleted.Should().BeFalse();
        await reader.CommitAsync();
        await using var revoke = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        await revoke.ExecuteStatementAsync(async (c, t) =>
        {
            var grant = new StoragePermission
            {
                Principal = Principal,
                ObjectId = binding.ObjectId,
                Action = StoragePermissionAction.Update,
                Effect = StoragePermissionEffect.Grant
            };
            await c.ReadSecurity().SetPermissionAsync(grant, true, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
            await c.ReadSecurity().SetPermissionAsync(grant, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
        });
        await revoke.CommitAsync();
        await using var stale = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        Func<Task> denied = async () => await stale.ExecuteStatementAsync((c, t) => c.ReadSecurity().ExecuteAuthorizedAsync(Request(binding, revision, Guid.NewGuid()), (_, _) => throw new Exception("Must not execute"), t));
        await denied.Should().ThrowAsync<StorageAuthorizationException>();
    }

    [Test]
    public async Task DenyOverridesGrantAndEscapedContextIsRejected()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var binding = await Provision(db);
        IStorageSecurityContext escaped = null!;
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync(async (c, t) =>
        {
            escaped = c.ReadSecurity();
            await c.ReadSecurity().SetPermissionAsync(new sql_storage_engine.Models.Security.StoragePermission { Principal = Principal, ObjectId = binding.ObjectId, Action = StoragePermissionAction.Update, Effect = StoragePermissionEffect.Deny }, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
        });
        Action outside = () => _ = escaped.ReadSnapshot();
        outside.Should().Throw<InvalidOperationException>();
        Func<Task> denied = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().ExecuteAuthorizedAsync(Request(binding, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid()), (_, _) => throw new Exception("Must not execute"), t));
        await denied.Should().ThrowAsync<StorageAuthorizationException>();
    }

    [Test]
    public async Task CancellationAfterDataChangeAbortsWholeRoot()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var binding = await Provision(db);
        var operation = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        Func<Task> canceled = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().ExecuteAuthorizedAsync(Request(binding, c.ReadSecurity().ReadSnapshot().Revision, operation), async (x, y) =>
        {
            await Change(x, db, y);
            cancellation.Cancel();
        }, t), cancellation.Token);
        await canceled.Should().ThrowAsync<OperationCanceledException>();
        root.ReadState().Should().Be(StorageTransactionState.Aborted);
        (await db.BalancesAsync()).Should().Equal(100, 100);
        (await db.Engine.ReadSecurity().ReadAsync()).MutationAudit.Should().NotContain(a => a.OperationId == operation);
    }

    [Test]
    public async Task AccessLimitAndForeignOrStaleBindingsRejectBeforeEffects()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var binding = await Provision(db);
        await using var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30)));
        await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().ExecuteAuthorizedAsync(StorageAuthorizationRequestExposure.CreateStorageAuthorizationRequest(Principal, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), Guid.NewGuid(), Enumerable.Repeat(new StorageRequiredAccess { Object = binding, Action = StoragePermissionAction.Update }, 256)), (_, _) => ValueTask.CompletedTask, t));
        Func<Task> over = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().ExecuteAuthorizedAsync(StorageAuthorizationRequestExposure.CreateStorageAuthorizationRequest(Principal, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), Guid.NewGuid(), Enumerable.Repeat(new StorageRequiredAccess { Object = binding, Action = StoragePermissionAction.Update }, 257)), (_, _) => throw new Exception("Must not execute"), t));
        await over.Should().ThrowAsync<ArgumentException>();
        foreach (var invalid in new[]
        {
            binding with
            {
                ObjectId = Guid.NewGuid()
            },
            binding with
            {
                SchemaVersion = binding.SchemaVersion + 1
            },
            binding with
            {
                DatabaseId = DatabaseIdExposure.CreateDatabaseId()
            }
        }

        )
        {
            Func<Task> stale = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().ExecuteAuthorizedAsync(Request(invalid, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid()), (_, _) => throw new Exception("Must not execute"), t));
            await stale.Should().ThrowAsync<StorageAuthorizationException>();
        }
    }

    [Test]
    public async Task ReadAuditFailurePreventsCallbackAndRootDisposalRollsBackPrincipal()
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync();
        var binding = await Provision(db, StoragePermissionAction.Select);
        db.Engine.WriteTransactionObserver(observer: stage =>
        {
            if (stage == "security-event-before-write")
                throw new IOException();
        });
        await using (var root = await db.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(30))))
        {
            Func<Task> failed = async () => await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().ExecuteAuthorizedAsync(Request(binding, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), StoragePermissionAction.Select), (_, _) => throw new Exception("Must not execute"), t));
            await failed.Should().ThrowAsync<StorageAuditException>();
            await root.ExecuteStatementAsync((c, t) => c.ReadSecurity().SetPrincipalAsync(new sql_storage_engine.Models.Security.StoragePrincipal { Id = Principal, Active = false }, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t));
        }

        db.Engine.WriteTransactionObserver(observer: null);
        (await db.Engine.ReadSecurity().ReadAsync()).Principals.Single().Active.Should().BeTrue();
        (await db.Engine.ReadSecurity().ReadEventsAsync()).Should().BeEmpty();
    }
}