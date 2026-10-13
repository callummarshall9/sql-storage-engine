// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Coordinations.TableDefinitions;
internal sealed partial class TableDefinitionCoordinationService
{
    private static void ValidateTableDefinition(TableDefinition tableDefinition)
    {
        ArgumentNullException.ThrowIfNull(argument: tableDefinition);
    }
}