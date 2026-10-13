// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.TableDefinitions;
internal interface ITableDefinitionCoordinationService
{
    TableDefinition PrepareTableDefinition(TableDefinition tableDefinition);

    void CheckTableDefinitionRow(TableDefinition tableDefinition);
}