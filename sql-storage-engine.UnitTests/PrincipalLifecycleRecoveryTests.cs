using AwesomeAssertions;
using sql_storage_engine.Security;

namespace sql_storage_engine.UnitTests;

public sealed class PrincipalLifecycleRecoveryTests
{
    [TestCase("security-mutation-before-audit")]
    [TestCase("security-mutation-after-audit")]
    [TestCase("cancel")]
    public async Task FaultOrCancellationCannotLeaveAnUnauditedChange(string stage)
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); await PrincipalLifecycleTests.Bootstrap(db);
        var before = await db.Engine.Security.ReadAsync(); var target = PrincipalLifecycleTests.Target();
        await using (var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30))))
        {
            using var cancel = new CancellationTokenSource();
            db.Engine.TransactionObserver = s => { if (s == stage) throw new IOException("fault"); if (stage == "cancel" && s == "security-mutation-before-audit") cancel.Cancel(); };
            Func<Task> failed = async () => await root.ExecuteStatementAsync((c, t) => c.Security.ChangePrincipalAsync(PrincipalLifecycleTests.Request(db, c, target, StoragePrincipalOperation.Create), cancel.Token));
            await failed.Should().ThrowAsync<Exception>(); db.Engine.TransactionObserver = null;
            if (stage == "cancel") { Func<Task> commit = async () => await root.CommitAsync(); await commit.Should().ThrowAsync<InvalidOperationException>(); }
            else await root.CommitAsync();
        }
        await db.ReopenAsync(); (await db.Engine.Security.ReadAsync()).Should().BeEquivalentTo(before);
    }
    [TestCase(true)]
    [TestCase(false)]
    public async Task SavepointAndDisposalRestoreTombstonesPermissionsAndAudit(bool commit)
    {
        await using var db = await ExplicitTransactionTestDatabase.CreateAsync(); await PrincipalLifecycleTests.Bootstrap(db); var target = PrincipalLifecycleTests.Target();
        await using (var root = await db.Engine.BeginTransactionAsync(new(TimeSpan.FromSeconds(30))))
        {
            await root.ExecuteStatementAsync((c, t) => c.Security.ChangePrincipalAsync(PrincipalLifecycleTests.Request(db, c, target, StoragePrincipalOperation.Create), t));
            var point = await root.CreateSavepointAsync("before-retirement");
            await root.ExecuteStatementAsync((c, t) => c.Security.ChangePrincipalAsync(PrincipalLifecycleTests.Request(db, c, target, StoragePrincipalOperation.Retire), t));
            await root.RollbackToSavepointAsync(point);
            if (commit) await root.CommitAsync();
        }
        await db.ReopenAsync(); var snapshot = await db.Engine.Security.ReadAsync(); snapshot.RetiredPrincipals.Should().BeEmpty(); snapshot.Principals.Any(p => p.Id == target).Should().Be(commit);
        snapshot.MutationAudit.Should().NotContain(a => a.PrincipalOperation == StoragePrincipalOperation.Retire);
    }
}
