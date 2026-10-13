// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Processings.TableDefinitions;
using sql_storage_engine.Services.Processings.Rows;

namespace sql_storage_engine.Services.Orchestrations.TableDefinitions;
internal sealed partial class TableDefinitionOrchestrationService(TableDefinitionProcessingService definitions, RowProcessingService rows) : ITableDefinitionOrchestrationService
{
    public TableDefinition PrepareTableDefinition(TableDefinition tableDefinition) =>
        TryCatch(operation: () =>
    {
        ValidateTableDefinition(tableDefinition: tableDefinition);
        return definitions.PrepareTableDefinition(tableDefinition: tableDefinition);
    });

    public TableDefinition CheckTableDefinitionRowMetadata(TableDefinition tableDefinition) =>
        TryCatch(operation: () =>
    {
        ValidateRow(tableDefinition: tableDefinition);

        var prepared = tableDefinition with
        {
            RowInput = rows.PrepareRow(row: tableDefinition.RowInput!)
        };

        definitions.CheckTableDefinitionRow(tableDefinition: prepared);
        return prepared;
    });

    internal static TableDefinitionOrchestrationService CreateTableDefinitionOrchestrationService() =>
        new TableDefinitionOrchestrationService(definitions: new sql_storage_engine.Services.Processings.TableDefinitions.TableDefinitionProcessingService(), rows: new sql_storage_engine.Services.Processings.Rows.RowProcessingService());
}