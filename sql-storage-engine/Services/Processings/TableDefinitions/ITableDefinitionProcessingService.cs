// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.TableDefinitions;
internal interface ITableDefinitionProcessingService
{
    TableDefinition PrepareTableDefinition(TableDefinition tableDefinition);

    void CheckTableDefinitionRow(TableDefinition tableDefinition);
}