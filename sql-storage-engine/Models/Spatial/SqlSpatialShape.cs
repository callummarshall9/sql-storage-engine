// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Spatial;
internal sealed class SqlSpatialShape
{
    public SqlSpatialShapeType Type { get; init; }
    public SqlSpatialCoordinate[] Points { get => points!; init => points = value; }
    public SqlSpatialShape[] Children { get => children!; init => children = value; }

    private SqlSpatialCoordinate[]? points;
    private SqlSpatialShape[]? children;
    public bool IsEmpty { get; init; }
    public IEnumerable<SqlSpatialCoordinate>? PointSource { get; init; }
    public IEnumerable<SqlSpatialShape>? ChildSource { get; init; }
}