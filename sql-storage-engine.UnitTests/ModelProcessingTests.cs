using AwesomeAssertions;
using System.Collections;
using sql_storage_engine.Exposures.Security;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Security;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;
public sealed class ModelProcessingTests
{
    [TestCase(0L, StorageTransactionOptions.MaximumSupportedJournalBytes, 0)]
    [TestCase(1L, 0L, 0)]
    [TestCase(1L, StorageTransactionOptions.MaximumSupportedJournalBytes, 1)]
    public async Task DirectlyInitializedOptionsAreValidatedBeforeAcquiringATransaction(long timeoutTicks, long journalBytes, int isolation)
    {
        await using var database = await ExplicitTransactionTestDatabase.CreateAsync();
        var invalid = new StorageTransactionOptions
        {
            Timeout = TimeSpan.FromTicks(timeoutTicks),
            MaximumJournalBytes = journalBytes,
            Isolation = (StorageTransactionIsolation)isolation
        };
        Func<Task> begin = async () =>
        {
            await using var transaction = await database.Engine.BeginTransactionAsync(invalid);
        };
        var failure = await begin.Should().ThrowAsync<StorageModelValidationException>();
        failure.Which.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        await using var next = await database.Engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromSeconds(5)));
        (await next.RollbackAsync()).State.Should().Be(StorageTransactionState.Aborted);
    }

    [Test]
    public void PreparingARowUpdateCreatesAnImmutableSnapshotOfCallerOwnedColumns()
    {
        var original = new ColumnUpdate
        {
            ColumnIndex = 1,
            Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(42)
        };
        var replacement = new ColumnUpdate
        {
            ColumnIndex = 2,
            Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(99)
        };
        ColumnUpdate[] callerColumns = [original];
        var update = RowUpdateExposure.PrepareRowUpdate(new RowUpdate { Columns = callerColumns });
        callerColumns[0] = replacement;
        update.Columns.Should().Equal(original);
        Action mutate = () => ((IList<ColumnUpdate>)update.Columns)[0] = replacement;
        mutate.Should().Throw<NotSupportedException>();
        update.Columns.Should().Equal(original);
    }

    [Test]
    public void MissingRowUpdateColumnsPreserveTheValidationCause()
    {
        Action create = () => RowUpdateExposure.CreateRowUpdate(null!);
        var failure = create.Should().Throw<StorageModelValidationException>();
        var cause = failure.Which.InnerException.Should().BeOfType<ArgumentNullException>().Subject;
        cause.ParamName.Should().Be("columns");
    }

    [Test]
    public void RowPreparationEnumeratesLazyValuesOnceAndFreezesTheSnapshot()
    {
        var source = new OnePassValues();
        var row = RowExposure.CreateRow(source);
        source.EnumerationCount.Should().Be(1);
        row.Values.Should().Equal(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(42));
        Action mutate = () => ((IList<SqlValue>)row.Values)[0] = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(99);
        mutate.Should().Throw<NotSupportedException>();
    }

    [Test]
    public void RowPreparationRejectsClrNullsAndPreservesTheirValidationCause()
    {
        Action create = () => RowExposure.CreateRow([null!]);
        var failure = create.Should().Throw<StorageModelValidationException>();
        failure.Which.InnerException.Should().BeOfType<ArgumentException>().Which.ParamName.Should().Be("values");
    }

    [Test]
    public void AuthorizationPreparationBoundsAndDisposesAnInfiniteAccessSource()
    {
        var enumerated = 0;
        var disposed = false;
        var access = new StorageRequiredAccess
        {
            Object = new StorageObjectBinding
            {
                ObjectId = Guid.NewGuid(),
                SchemaVersion = 1
            },
            Action = StoragePermissionAction.Select
        };
        IEnumerable<StorageRequiredAccess> Infinite()
        {
            try
            {
                while (true)
                {
                    enumerated++;
                    yield return access;
                }
            }
            finally
            {
                disposed = true;
            }
        }

        var request = StorageAuthorizationRequestExposure.CreateStorageAuthorizationRequest(default, 1, Guid.NewGuid(), Guid.NewGuid(), Infinite());
        request.Access.Should().HaveCount(257);
        enumerated.Should().Be(257);
        disposed.Should().BeTrue();
        Action mutate = () => ((IList<StorageRequiredAccess>)request.Access)[0] = access;
        mutate.Should().Throw<NotSupportedException>();
    }

    [Test]
    public void SecuritySnapshotPreparationCopiesAllCallerOwnedCollections()
    {
        var principalId = new StoragePrincipalId
        {
            Issuer = Guid.NewGuid(),
            Subject = Guid.NewGuid()
        };
        var principal = new StoragePrincipal
        {
            Id = principalId,
            Active = true
        };
        var permission = new StoragePermission
        {
            Principal = principalId,
            ObjectId = Guid.NewGuid(),
            Action = StoragePermissionAction.Select,
            Effect = StoragePermissionEffect.Grant
        };
        var databasePermission = new StorageDatabasePermission
        {
            Principal = principalId,
            Action = StorageDatabasePermissionAction.ManagePrincipals,
            Effect = StoragePermissionEffect.Grant
        };
        var reference = new StoragePrincipalReference
        {
            Principal = principalId,
            ReferenceId = Guid.NewGuid()
        };
        var audit = new sql_storage_engine.Models.Security.StorageAuditRecord{OperationId=Guid.NewGuid(),CorrelationId=Guid.NewGuid(),Principal=principalId,ObjectId=null,Action=StoragePermissionAction.ManagePermissions,Kind=StorageAuditKind.SecurityChange,Revision=7};
        StoragePrincipal[] principals = [principal];
        StoragePrincipalId[] retired = [principalId];
        StoragePermission[] permissions = [permission];
        StorageDatabasePermission[] databasePermissions = [databasePermission];
        StoragePrincipalReference[] references = [reference];
        StorageAuditRecord[] audits = [audit];
        var snapshot = StorageSecuritySnapshotExposure.PrepareStorageSecuritySnapshot(new StorageSecuritySnapshot { Revision = 7, Principals = principals, RetiredPrincipals = retired, Permissions = permissions, DatabasePermissions = databasePermissions, PrincipalDependencies = references, MutationAudit = audits });
        principals[0] = principal with
        {
            Active = false
        };
        retired[0] = default;
        permissions[0] = permission with
        {
            Effect = StoragePermissionEffect.Deny
        };
        databasePermissions[0] = databasePermission with
        {
            Effect = StoragePermissionEffect.Deny
        };
        references[0] = reference with
        {
            ReferenceId = Guid.NewGuid()
        };
        audits[0] = new sql_storage_engine.Models.Security.StorageAuditRecord{OperationId=Guid.NewGuid(),CorrelationId=Guid.NewGuid(),Principal=null,ObjectId=null,Action=StoragePermissionAction.ManagePermissions,Kind=StorageAuditKind.SecurityChange,Revision=8};
        snapshot.Revision.Should().Be(7);
        snapshot.Principals.Should().Equal(principal);
        snapshot.RetiredPrincipals.Should().Equal(principalId);
        snapshot.Permissions.Should().Equal(permission);
        snapshot.DatabasePermissions.Should().Equal(databasePermission);
        snapshot.PrincipalDependencies.Should().Equal(reference);
        snapshot.MutationAudit.Should().Equal(audit);
        ((ICollection<StoragePrincipal>)snapshot.Principals).IsReadOnly.Should().BeTrue();
        ((ICollection<StoragePrincipalId>)snapshot.RetiredPrincipals).IsReadOnly.Should().BeTrue();
        ((ICollection<StoragePermission>)snapshot.Permissions).IsReadOnly.Should().BeTrue();
        ((ICollection<StorageDatabasePermission>)snapshot.DatabasePermissions).IsReadOnly.Should().BeTrue();
        ((ICollection<StoragePrincipalReference>)snapshot.PrincipalDependencies).IsReadOnly.Should().BeTrue();
        ((ICollection<StorageAuditRecord>)snapshot.MutationAudit).IsReadOnly.Should().BeTrue();
    }

    private sealed class OnePassValues : IEnumerable<SqlValue>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<SqlValue> GetEnumerator()
        {
            EnumerationCount++;
            if (EnumerationCount != 1)
                throw new InvalidOperationException("The values were enumerated twice.");
            yield return sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(42);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}