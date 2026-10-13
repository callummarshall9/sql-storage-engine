// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Processings.TableDefinitions;
using sql_storage_engine.Services.Processings.Rows;
using sql_storage_engine.Services.Coordinations.TableDefinitions;
using sql_storage_engine.Services.Orchestrations.TableDefinitions;
using sql_storage_engine.Services.Orchestrations.SqlTypes;
using sql_storage_engine.Services.Foundations.SqlCollations;
using sql_storage_engine.Services.Foundations.SqlTypeDocuments;

namespace sql_storage_engine.Exposures.Rows;
public sealed class ColumnDefinitionExposure : cCoder.CodeAnalysis.Exposures.ICompositionExposure
{
    private ColumnDefinitionExposure()
    {
    }

    private static readonly ITableDefinitionCoordinationService service = new TableDefinitionCoordinationService(definitions: new TableDefinitionOrchestrationService(definitions: new TableDefinitionProcessingService(), rows: new RowProcessingService()), types: new SqlTypeContextOrchestrationService(sqlCollationService: new SqlCollationService(), sqlTypeDocumentService: new SqlTypeDocumentService(), sqlDecimalService: new sql_storage_engine.Services.Foundations.SqlDecimals.SqlDecimalService()), values: new sql_storage_engine.Services.Orchestrations.SqlTypeValues.SqlTypeValueContextOrchestrationService(hierarchies: new sql_storage_engine.Services.Foundations.SqlHierarchies.SqlHierarchyIdService(), spatial: new sql_storage_engine.Services.Foundations.SpatialBinaries.SqlSpatialShapeBinaryService(), clr: new sql_storage_engine.Services.Foundations.SqlClrContexts.SqlClrContextService(broker: new sql_storage_engine.Brokers.ClrRuntimes.SqlClrRuntimeBroker())));
    public static ColumnDefinition CreateColumnDefinition(ColumnId columnId, string name, SqlType sqlType, bool isNullable, CatalogColumnEncryption? catalogColumnEncryption = null, bool isSparse = false, bool isColumnSet = false) =>
        service.PrepareTableDefinition(tableDefinition: new TableDefinition { Columns = [new ColumnDefinition { Id = columnId, Name = name, Type = sqlType, IsNullable = isNullable, Encryption = catalogColumnEncryption, IsSparse = isSparse, IsColumnSet = isColumnSet }] })
            .Columns[0];

    public static void CheckColumnDefinitionValue(ColumnDefinition columnDefinition, SqlValue sqlValue) =>
        service.CheckTableDefinitionRow(tableDefinition: new TableDefinition { SchemaSource = new TableDefinition { Columns = [columnDefinition] }, RowInput = new Row { Values = [sqlValue] } });
}