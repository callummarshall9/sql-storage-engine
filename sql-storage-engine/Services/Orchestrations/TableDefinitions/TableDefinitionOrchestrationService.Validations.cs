// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Orchestrations.TableDefinitions;
internal sealed partial class TableDefinitionOrchestrationService
{
    private static void ValidateTableDefinition(TableDefinition tableDefinition)
    {
        ArgumentNullException.ThrowIfNull(argument: tableDefinition);
    }

    private static void ValidateRow(TableDefinition tableDefinition)
    {
        ValidateTableDefinition(tableDefinition: tableDefinition);
        ArgumentNullException.ThrowIfNull(argument: tableDefinition.RowInput, paramName: "row");
    }
}