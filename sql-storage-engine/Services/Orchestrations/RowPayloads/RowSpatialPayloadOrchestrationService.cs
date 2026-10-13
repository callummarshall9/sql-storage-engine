// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Processings.SpatialWkts;
using sql_storage_engine.Services.Processings.SpatialShapes;
using sql_storage_engine.Services.Processings.SpatialBinaries;

namespace sql_storage_engine.Services.Orchestrations.RowPayloads;
internal sealed partial class RowSpatialPayloadOrchestrationService : IRowSpatialPayloadOrchestrationService
{
    private readonly SqlSpatialShapeWktProcessingService wkt;
    private readonly SqlSpatialShapeProcessingService shapes;
    private readonly SqlSpatialShapeBinaryProcessingService binary;
    public RowSpatialPayloadOrchestrationService(SqlSpatialShapeWktProcessingService wkt, SqlSpatialShapeProcessingService shapes, SqlSpatialShapeBinaryProcessingService binary)
    {
        this.wkt = wkt;
        this.shapes = shapes;
        this.binary = binary;
    }

    public RowPayloadContext WriteRowPayloadContextSpatial(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateValue(rowPayloadContext: rowPayloadContext);
        var value = ((SpatialSqlValue)rowPayloadContext.Value!).Value;
        var shape = shapes.PrepareSqlSpatialShape(sqlSpatialShape: value.Shape ?? (value.ShapeContent is null ? wkt.ParseSqlSpatialShape(source: value.WellKnownText) : shapes.ReadSqlSpatialShapeContent(content: value.ShapeContent)));
        ValidateSpatialKind(shape: shape, kind: value.Kind);
        rowPayloadContext.Bytes = binary.EncodeSqlSpatialShape(kind: value.Kind, srid: value.Srid, sqlSpatialShape: shape);
        return rowPayloadContext;
    });

    public RowPayloadContext ReadRowPayloadContextSpatial(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(rowPayloadContext: rowPayloadContext);
        var decoded = binary.DecodeSqlSpatialShape(value: rowPayloadContext.Source!);
        var expectedKind = rowPayloadContext.Column!.Type.Name == SqlTypeName.Geometry ? SqlSpatialKind.Geometry : SqlSpatialKind.Geography;

        if (decoded.Kind != expectedKind)
        {
            throw new StorageFormatException(message: "Spatial payload kind does not match its declared SQL type.");
        }

        var shape = shapes.PrepareSqlSpatialShape(sqlSpatialShape: decoded.Shape);
        ValidateSpatialKind(shape: shape, kind: decoded.Kind);

        rowPayloadContext.Value = new SpatialSqlValue
        {
            Value = new SqlSpatial
            {
                Kind = decoded.Kind,
                Srid = decoded.Srid,
                ShapeContent = shapes.WriteSqlSpatialShapeContent(sqlSpatialShape: shape),
                WellKnownText = shapes.FormatSqlSpatialShape(sqlSpatialShape: shape)
            },
            StorageFamily = SqlStorageFamily.Binary
        };

        return rowPayloadContext;
    });

    public RowPayloadContext ReadRowPayloadContextBounds(RowPayloadContext rowPayloadContext) =>
        TryCatch(operation: () =>
    {
        ValidateValue(rowPayloadContext: rowPayloadContext);
        var value = ((SpatialSqlValue)rowPayloadContext.Value!).Value;
        var shape = shapes.PrepareSqlSpatialShape(sqlSpatialShape: value.Shape ?? (value.ShapeContent is null ? wkt.ParseSqlSpatialShape(source: value.WellKnownText) : shapes.ReadSqlSpatialShapeContent(content: value.ShapeContent)));
        ValidateSpatialKind(shape: shape, kind: value.Kind);
        rowPayloadContext.Bounds = shapes.ReadSqlSpatialShapeBounds(sqlSpatialShape: shape);
        return rowPayloadContext;
    });

    internal static RowSpatialPayloadOrchestrationService CreateRowSpatialPayloadOrchestrationService() =>
        new RowSpatialPayloadOrchestrationService(wkt: new SqlSpatialShapeWktProcessingService(), shapes: new SqlSpatialShapeProcessingService(), binary: SqlSpatialShapeBinaryProcessingService.CreateSqlSpatialShapeBinaryProcessingService());
}