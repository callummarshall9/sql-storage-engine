// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.TableDefinitions;
internal interface ITableDefinitionOrchestrationService
{
    TableDefinition PrepareTableDefinition(TableDefinition tableDefinition);

    TableDefinition CheckTableDefinitionRowMetadata(TableDefinition tableDefinition);
}