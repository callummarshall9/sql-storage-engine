// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Text.Json;
using sql_storage_engine;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Security;
using sql_storage_engine.Models;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(args[0]);
        var transaction = await engine.BeginTransactionAsync(StorageTransactionOptionsExposure.CreateStorageTransactionOptions(TimeSpan.FromMinutes(1)));
        await File.WriteAllTextAsync(args[0] + ".probe-identity", JsonSerializer.Serialize(transaction.ReadIdentity()));
        await transaction.ExecuteStatementAsync(async (context, token) =>
        {
            var definition = await context.CreateCatalogTableAsync(sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.CreateCatalogTableName("master","dbo","crash_created"), [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)], cancellationToken: token);
            await (await context.OpenTableAsync(definition.Id, token)).InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(42)]), token);
        });
        if (args[1].StartsWith("lifecycle", StringComparison.Ordinal))
        {
            var actor = new sql_storage_engine.Models.Security.StoragePrincipalId
            {
                Issuer = Guid.NewGuid(),
                Subject = Guid.NewGuid()
            };
            var target = new sql_storage_engine.Models.Security.StoragePrincipalId
            {
                Issuer = Guid.NewGuid(),
                Subject = Guid.NewGuid()
            };
            await transaction.ExecuteStatementAsync(async (c, t) =>
            {
                await c.ReadSecurity().SetPrincipalAsync(new sql_storage_engine.Models.Security.StoragePrincipal { Id = actor, Active = true }, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
                await c.ReadSecurity().SetDatabasePermissionAsync(new sql_storage_engine.Models.Security.StorageDatabasePermission { Principal = actor, Action = sql_storage_engine.Security.StorageDatabasePermissionAction.ManagePrincipals, Effect = sql_storage_engine.Security.StoragePermissionEffect.Grant }, false, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t);
                await c.ReadSecurity().ChangePrincipalAsync(new sql_storage_engine.Models.Security.StoragePrincipalChange { DatabaseId = engine.ReadDatabaseId(), Actor = actor, Target = target, Operation = sql_storage_engine.Security.StoragePrincipalOperation.Create, Revision = c.ReadSecurity().ReadSnapshot().Revision, OperationId = Guid.NewGuid(), CorrelationId = Guid.NewGuid() }, t);
                await c.ReadSecurity().ChangePrincipalAsync(new sql_storage_engine.Models.Security.StoragePrincipalChange { DatabaseId = engine.ReadDatabaseId(), Actor = actor, Target = target, Operation = sql_storage_engine.Security.StoragePrincipalOperation.Retire, Revision = c.ReadSecurity().ReadSnapshot().Revision, OperationId = Guid.NewGuid(), CorrelationId = Guid.NewGuid() }, t);
            });
            if (args[1] == "lifecycle-committed")
                await transaction.CommitAsync();
            Environment.Exit(42);
        }

        if (args[1].StartsWith("security", StringComparison.Ordinal))
        {
            await transaction.ExecuteStatementAsync((c, t) => c.ReadSecurity().SetPrincipalAsync(new sql_storage_engine.Models.Security.StoragePrincipal { Id = new sql_storage_engine.Models.Security.StoragePrincipalId { Issuer = Guid.Parse("11111111-2222-3333-4444-555555555555"), Subject = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") }, Active = true }, c.ReadSecurity().ReadSnapshot().Revision, Guid.NewGuid(), t));
            if (args[1].StartsWith("security-event-", StringComparison.Ordinal))
                engine.WriteTransactionObserver(observer: stage =>
                {
                    if (stage == args[1])
                        Environment.Exit(42);
                });
            await engine.ReadSecurity().AppendEventAsync(new sql_storage_engine.Models.Security.StorageAuditRecord { OperationId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), Action = sql_storage_engine.Security.StoragePermissionAction.Select, Kind = sql_storage_engine.Security.StorageAuditKind.ReadAdmission, Revision = 2 });
            if (args[1] == "security-committed")
                await transaction.CommitAsync();
            Environment.Exit(42);
        }

        if (args[1].StartsWith("savepoint", StringComparison.Ordinal))
        {
            var point = await transaction.CreateStorageSavepointAsync("before");
            await transaction.ExecuteStatementAsync(async (context, token) => await context.CreateCatalogTableAsync(sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.CreateCatalogTableName("master","dbo","savepoint_later"), [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)], cancellationToken: token));
            if (args[1] == "savepoint-rewrite")
                engine.WriteTransactionObserver(observer: stage =>
                {
                    if (stage == "AfterSavepointRestore")
                        Environment.Exit(42);
                });
            if (args[1] != "savepoint-active")
                await transaction.RollbackToSavepointAsync(point);
            if (args[1] == "savepoint-committed")
                await transaction.CommitAsync();
            Environment.Exit(42);
        }

        if (args[1] == "committed")
            await transaction.CommitAsync();
        else if (args[1] == "nested")
            await transaction.ExecuteStatementAsync((_, _) =>
            {
                Environment.Exit(42);
                return ValueTask.CompletedTask;
            });
        // Deliberately terminate without disposing the engine or transaction.
        Environment.Exit(42);
    }
}