// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Orchestrations.TableDefinitions;
using sql_storage_engine.Services.Orchestrations.SqlTypes;

namespace sql_storage_engine.Services.Coordinations.TableDefinitions;
internal sealed partial class TableDefinitionCoordinationService(TableDefinitionOrchestrationService definitions, SqlTypeContextOrchestrationService types, sql_storage_engine.Services.Orchestrations.SqlTypeValues.SqlTypeValueContextOrchestrationService values) : ITableDefinitionCoordinationService
{
    public TableDefinition PrepareTableDefinition(TableDefinition tableDefinition) =>
        TryCatch(operation: () =>
    {
        ValidateTableDefinition(tableDefinition: tableDefinition);
        var prepared = definitions.PrepareTableDefinition(tableDefinition: tableDefinition);

        foreach (var column in prepared.Columns)
        {
            if (!types.ProcessSqlTypeContextCanBeColumn4(sqlTypeContext: new SqlTypeContext { SqlTypeSource = column.Type })
            .BooleanResult)
            {
                throw new ArgumentException(message: $"{column.Type.Name} cannot be used as a table column.", paramName: "type");
            }
        }

        return prepared;
    });

    public void CheckTableDefinitionRow(TableDefinition tableDefinition) =>
        TryCatch(operation: () =>
    {
        ValidateTableDefinition(tableDefinition: tableDefinition);
        var prepared = definitions.CheckTableDefinitionRowMetadata(tableDefinition: tableDefinition);
        var schema = prepared.SchemaSource!;

        for (var index = 0; index < schema.Columns.Count; index++)
        {
            types.ProcessSqlTypeContextValidate76(sqlTypeContext: new SqlTypeContext { SqlTypeSource = schema.Columns[index].Type, InputSqlValueValue = prepared.RowInput!.Values[index], InputstringColumnName = schema.Columns[index].Name });
            values.CheckSqlTypeContext(sqlTypeContext: new SqlTypeContext { SqlTypeSource = schema.Columns[index].Type, InputSqlValueValue = prepared.RowInput!.Values[index], InputstringColumnName = schema.Columns[index].Name });
        }
    });

    internal static TableDefinitionCoordinationService CreateTableDefinitionCoordinationService() =>
        new(definitions: TableDefinitionOrchestrationService.CreateTableDefinitionOrchestrationService(), types: SqlTypeContextOrchestrationService.CreateSqlTypeContextOrchestrationService(), values: sql_storage_engine.Services.Orchestrations.SqlTypeValues.SqlTypeValueContextOrchestrationService.CreateSqlTypeValueContextOrchestrationService());
}