// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Processings.TableDefinitions;
using sql_storage_engine.Services.Processings.Rows;
using sql_storage_engine.Services.Coordinations.TableDefinitions;
using sql_storage_engine.Services.Orchestrations.TableDefinitions;
using sql_storage_engine.Services.Orchestrations.SqlTypes;
using sql_storage_engine.Services.Foundations.SqlCollations;
using sql_storage_engine.Services.Foundations.SqlTypeDocuments;

namespace sql_storage_engine.Exposures.Rows;
public sealed class TableDefinitionExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private TableDefinitionExposure()
    {
    }

    private static readonly ITableDefinitionCoordinationService service = new TableDefinitionCoordinationService(definitions: new TableDefinitionOrchestrationService(definitions: new TableDefinitionProcessingService(), rows: new RowProcessingService()), types: new SqlTypeContextOrchestrationService(sqlCollationService: new SqlCollationService(), sqlTypeDocumentService: new SqlTypeDocumentService(), sqlDecimalService: new sql_storage_engine.Services.Foundations.SqlDecimals.SqlDecimalService()), values: new sql_storage_engine.Services.Orchestrations.SqlTypeValues.SqlTypeValueContextOrchestrationService(hierarchies: new sql_storage_engine.Services.Foundations.SqlHierarchies.SqlHierarchyIdService(), spatial: new sql_storage_engine.Services.Foundations.SpatialBinaries.SqlSpatialShapeBinaryService(), clr: new sql_storage_engine.Services.Foundations.SqlClrContexts.SqlClrContextService(broker: new sql_storage_engine.Brokers.ClrRuntimes.SqlClrRuntimeBroker())));
    public static TableDefinition CreateTableDefinition(IEnumerable<ColumnDefinition> columnDefinitions) =>
        service.PrepareTableDefinition(tableDefinition: new TableDefinition { ColumnSource = columnDefinitions });

    public static void CheckTableDefinitionRow(TableDefinition tableDefinition, Row row) =>
        service.CheckTableDefinitionRow(tableDefinition: new TableDefinition { SchemaSource = tableDefinition, RowInput = row });
}