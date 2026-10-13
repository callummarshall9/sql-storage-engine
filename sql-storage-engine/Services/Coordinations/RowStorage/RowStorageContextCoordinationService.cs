// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Services.Orchestrations.TableDefinitions;
using sql_storage_engine.Services.Orchestrations.OverflowChains;

namespace sql_storage_engine.Services.Coordinations.RowStorage;
internal sealed partial class RowStorageContextCoordinationService : IRowStorageContextCoordinationService
{
    private readonly sql_storage_engine.Services.Orchestrations.GraphRows.GraphRowOrchestrationService graphs;
    private readonly TableDefinitionOrchestrationService definitions;
    private readonly OverflowChainContextOrchestrationService overflow;
    private RowStorageContextCoordinationService(TableDefinitionOrchestrationService definitions, OverflowChainContextOrchestrationService overflow, sql_storage_engine.Services.Orchestrations.GraphRows.GraphRowOrchestrationService graphs)
    {
        this.graphs = graphs;
        this.definitions = definitions;
        this.overflow = overflow;
    }

    internal static RowStorageContextCoordinationService CreateRowStorageContextCoordinationService(RowEncodingConfiguration rowEncodingConfiguration) =>
        new RowStorageContextCoordinationService(graphs: sql_storage_engine.Services.Orchestrations.GraphRows.GraphRowOrchestrationService.CreateGraphRowOrchestrationService(), definitions: TableDefinitionOrchestrationService.CreateTableDefinitionOrchestrationService(), overflow: (OverflowChainContextOrchestrationService)rowEncodingConfiguration.OverflowService!);

    internal static RowStorageContextCoordinationService CreateRowStorageContextCoordinationService(OverflowPageResourceConfiguration overflowPageResourceConfiguration) =>
        new RowStorageContextCoordinationService(graphs: sql_storage_engine.Services.Orchestrations.GraphRows.GraphRowOrchestrationService.CreateGraphRowOrchestrationService(), definitions: TableDefinitionOrchestrationService.CreateTableDefinitionOrchestrationService(), overflow: OverflowChainContextOrchestrationService.CreateOverflowChainContextOrchestrationService(overflowPageResourceConfiguration: overflowPageResourceConfiguration));

    public RowEncodingContext CheckRowEncodingContextMetadata(RowEncodingContext rowEncodingContext) =>
        TryCatch(operation: () =>
    {
        ValidateMetadata(rowEncodingContext: rowEncodingContext);

        rowEncodingContext.Row = definitions.CheckTableDefinitionRowMetadata(tableDefinition: new TableDefinition { SchemaSource = rowEncodingContext.Table, RowInput = rowEncodingContext.Row })
            .RowInput;

        return rowEncodingContext;
    });

    public ValueTask<RowEncodingContext> WriteRowEncodingContextOverflowAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(rowEncodingContext: rowEncodingContext, cancellationToken: cancellationToken);

        rowEncodingContext.Reference = (await overflow!.WriteOverflowChainContextAsync(overflowChainContext: new OverflowChainContext { Value = rowEncodingContext.Source }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Reference;

        return rowEncodingContext;
    });

    public ValueTask<RowEncodingContext> ReadRowEncodingContextOverflowAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateReference(rowEncodingContext: rowEncodingContext, cancellationToken: cancellationToken);

        rowEncodingContext.Source = (await overflow!.ReadOverflowChainContextAsync(overflowChainContext: new OverflowChainContext { Reference = rowEncodingContext.Reference }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Value;

        return rowEncodingContext;
    });

    public ValueTask FreeRowEncodingContextOverflowAsync(RowEncodingContext rowEncodingContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateReference(rowEncodingContext: rowEncodingContext, cancellationToken: cancellationToken);

        await overflow!.FreeOverflowChainContextAsync(overflowChainContext: new OverflowChainContext { Reference = rowEncodingContext.Reference }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}