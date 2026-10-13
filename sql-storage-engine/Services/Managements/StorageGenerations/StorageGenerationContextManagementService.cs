// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Coordinations.PageDatabases;
using sql_storage_engine.Services.Coordinations.SqlTypes;
using sql_storage_engine.Services.Coordinations.SqlExpressions;

namespace sql_storage_engine.Services.Managements.StorageGenerations;
internal sealed partial class StorageGenerationContextManagementService : IStorageGenerationContextManagementService
{
    private readonly PageDatabaseContextCoordinationService database;
    private readonly SqlTypeContextCoordinationService types;
    private readonly SqlExpressionRuntimeCoordinationService environment;
    private readonly StorageGenerationContext state;
    private StorageGenerationContextManagementService(PageDatabaseContextCoordinationService database, SqlTypeContextCoordinationService types, SqlExpressionRuntimeCoordinationService environment, StorageGenerationConfiguration storageGenerationConfiguration)
    {
        this.database = database;
        this.types = types;
        this.environment = environment;
        state = new StorageGenerationContext
        {
            Database = storageGenerationConfiguration.DatabaseBinding!.Context,
            TimeProviderSource = storageGenerationConfiguration.TimeProviderSource
        };
    }

    internal static StorageGenerationContextManagementService CreateStorageGenerationContextManagementService(PageDatabaseBinding pageDatabaseBinding, object? timeProviderSource = null) =>
        new(database: (PageDatabaseContextCoordinationService)pageDatabaseBinding.Service!, types: SqlTypeContextCoordinationService.CreateSqlTypeContextCoordinationService(), environment: SqlExpressionRuntimeCoordinationService.CreateSqlExpressionRuntimeCoordinationService(), storageGenerationConfiguration: new StorageGenerationConfiguration { DatabaseBinding = pageDatabaseBinding, TimeProviderSource = timeProviderSource });

    internal static StorageGenerationContextManagementService CreateStorageGenerationContextManagementService(StorageGenerationConfiguration storageGenerationConfiguration) =>
        CreateStorageGenerationContextManagementService(pageDatabaseBinding: storageGenerationConfiguration.DatabaseBinding!, timeProviderSource: storageGenerationConfiguration.TimeProviderSource);

    public StorageGenerationContext ReadStorageGenerationContextTimestamp(StorageGenerationContext storageGenerationContext) =>
        TryCatch(operation: () =>
    {
        ValidateStorageGenerationContext(storageGenerationContext: storageGenerationContext, cancellationToken: CancellationToken.None);

        var timestamp = environment.ReadSqlExpressionContextTimestamp(sqlExpressionContext: new SqlExpressionContext { Utc = true, TimeProviderSource = state.TimeProviderSource })
            .Timestamp;

        return storageGenerationContext with
        {
            Timestamp = new DateTimeOffset(dateTime: DateTime.SpecifyKind(value: timestamp, kind: DateTimeKind.Utc))
        };
    });

    public ValueTask<StorageGenerationContext> AllocateStorageGenerationContextRowVersionAsync(StorageGenerationContext storageGenerationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageGenerationContext(storageGenerationContext: storageGenerationContext, cancellationToken: cancellationToken);

        var allocated = await database.AllocatePageDatabaseContextRowVersionAsync(pageDatabaseContext: state.Database!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var type = types.ProcessSqlTypeContextTimestamp22(sqlTypeContext: new SqlTypeContext())
            .SqlTypeResult;

        var value = types.ProcessSqlTypeContextNormalizeForStorage77(sqlTypeContext: new SqlTypeContext { SqlTypeSource = type, InputSqlValueValue = allocated.ResultValue })
            .SqlValueResult;

        return storageGenerationContext with
        {
            Value = value
        };
    });

    public ValueTask<StorageGenerationContext> AllocateStorageGenerationContextGeneratedValueAsync(StorageGenerationContext storageGenerationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageGenerationContextGenerated(storageGenerationContext: storageGenerationContext, cancellationToken: cancellationToken);
        SqlValue value;
        SqlType? type;

        if (storageGenerationContext.Kind is CatalogGeneratedAlwaysKind.RowStart or CatalogGeneratedAlwaysKind.RowEnd)
        {
            var timestamp = storageGenerationContext.Kind == CatalogGeneratedAlwaysKind.RowEnd ? DateTime.MaxValue : environment.ReadSqlExpressionContextTimestamp(sqlExpressionContext: new SqlExpressionContext { Utc = true, TimeProviderSource = state.TimeProviderSource })
            .Timestamp;

            value = new DateTimeSqlValue
            {
                Value = timestamp,
                StorageFamily = SqlStorageFamily.DateTime
            };

            type = types.ProcessSqlTypeContextDateTime256(sqlTypeContext: new SqlTypeContext { InputbyteScale = 7 })
            .SqlTypeResult;
        }
        else
        {
            var allocated = await database.AllocatePageDatabaseContextGeneratedValueAsync(pageDatabaseContext: state.Database!, kind: storageGenerationContext.Kind, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            value = allocated.ResultValue!;

            type = types.ProcessSqlTypeContextBigInt11(sqlTypeContext: new SqlTypeContext())
            .SqlTypeResult;
        }

        var admitted = types.ProcessSqlTypeContextNormalizeForStorage77(sqlTypeContext: new SqlTypeContext { SqlTypeSource = type, InputSqlValueValue = value })
            .SqlValueResult;

        return storageGenerationContext with
        {
            Value = admitted
        };
    });

    public ValueTask PublishStorageGenerationContextCatalogRootAsync(StorageGenerationContext storageGenerationContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateStorageGenerationContextPublication(storageGenerationContext: storageGenerationContext, cancellationToken: cancellationToken);

        await database.PublishPageDatabaseContextCatalogAsync(pageDatabaseContext: state.Database!, pageId: storageGenerationContext.RootPageId.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}