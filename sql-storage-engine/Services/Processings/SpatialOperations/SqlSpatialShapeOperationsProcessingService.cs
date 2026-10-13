// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Spatial;

namespace sql_storage_engine.Services.Processings.SpatialOperations;
internal sealed partial class SqlSpatialShapeOperationsProcessingService : ISqlSpatialShapeOperationsProcessingService
{
    public void CheckSqlSpatialShapeGeography(SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);
        ValidateGeography(shape: sqlSpatialShape);
    });

    public bool CheckValidSqlSpatialShape(SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);
        return IsValid(shape: sqlSpatialShape);
    });

    public (double MinX, double MinY, double MaxX, double MaxY) ReadSqlSpatialShapeBounds(SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);
        return Bounds(shape: sqlSpatialShape);
    });

    public SqlSpatialShape CreateSqlSpatialShapeEnvelope(SqlSpatialShape sqlSpatialShape) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShape(sqlSpatialShape: sqlSpatialShape);
        return Envelope(shape: sqlSpatialShape);
    });

    public double MeasureSqlSpatialShapeLength(SqlSpatialShape sqlSpatialShape, SqlSpatialKind kind) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapeMeasurement(sqlSpatialShape: sqlSpatialShape, kind: kind);
        return Length(shape: sqlSpatialShape, kind: kind);
    });

    public double MeasureSqlSpatialShapeArea(SqlSpatialShape sqlSpatialShape, SqlSpatialKind kind) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapeMeasurement(sqlSpatialShape: sqlSpatialShape, kind: kind);
        return Area(shape: sqlSpatialShape, kind: kind);
    });

    public bool CheckEquivalentSqlSpatialShapes(SqlSpatialShape[] sqlSpatialShapes) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapes(sqlSpatialShapes: sqlSpatialShapes);
        return EqualsTopologically(left: sqlSpatialShapes[0], right: sqlSpatialShapes[1]);
    });

    public bool CheckIntersectingSqlSpatialShapes(SqlSpatialShape[] sqlSpatialShapes) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapes(sqlSpatialShapes: sqlSpatialShapes);
        return Intersects(left: sqlSpatialShapes[0], right: sqlSpatialShapes[1]);
    });

    public bool CheckContainingSqlSpatialShapes(SqlSpatialShape[] sqlSpatialShapes) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapes(sqlSpatialShapes: sqlSpatialShapes);
        return Contains(container: sqlSpatialShapes[0], candidate: sqlSpatialShapes[1]);
    });

    public double MeasureSqlSpatialShapeDistance(SqlSpatialShape[] sqlSpatialShapes, SqlSpatialKind kind) =>
        TryCatch(operation: () =>
    {
        ValidateSqlSpatialShapePairMeasurement(sqlSpatialShapes: sqlSpatialShapes, kind: kind);
        return Distance(left: sqlSpatialShapes[0], right: sqlSpatialShapes[1], kind: kind);
    });

    private static IEnumerable<SqlSpatialCoordinate> Coordinates(SqlSpatialShape shape) =>
        shape.Points.Concat(second: shape.Children.SelectMany(selector: child => Coordinates(shape: child)));

    private static SqlSpatialShape CreateSqlSpatialShape(SqlSpatialShape sqlSpatialShape) =>
        new SqlSpatialShape
    {
        Type = sqlSpatialShape.Type,
        Points = sqlSpatialShape.PointSource?.ToArray() ?? Array.Empty<SqlSpatialCoordinate>(),
        Children = sqlSpatialShape.ChildSource?.ToArray() ?? Array.Empty<SqlSpatialShape>(),
        IsEmpty = sqlSpatialShape.IsEmpty
    };

    private static bool OverlapBounds((double MinX, double MinY, double MaxX, double MaxY) first, (double MinX, double MinY, double MaxX, double MaxY) second) =>
        first.MinX <= second.MaxX && first.MaxX >= second.MinX && first.MinY <= second.MaxY && first.MaxY >= second.MinY;

    private static bool ContainBounds((double MinX, double MinY, double MaxX, double MaxY) first, (double MinX, double MinY, double MaxX, double MaxY) second) =>
        first.MinX <= second.MinX && first.MinY <= second.MinY && first.MaxX >= second.MaxX && first.MaxY >= second.MaxY;
    private const double EarthRadius = 6_371_008.8;
    private static void ValidateGeography(SqlSpatialShape shape)
    {
        foreach (var point in Coordinates(shape: shape))
        {
            if (point.X is < -180 or > 180 || point.Y is < -90 or > 90)
            {
                throw new ArgumentOutOfRangeException(nameof(shape), "Geography longitude/latitude is outside its valid range.");
            }
        }
    }

    private static bool IsValid(SqlSpatialShape shape)
    {
        if (shape.IsEmpty || shape.Type == SqlSpatialShapeType.FullGlobe)
        {
            return true;
        }

        if (shape.Type is SqlSpatialShapeType.LineString && shape.Points.Length < 2)
        {
            return false;
        }

        if (shape.Type is SqlSpatialShapeType.CircularString && (shape.Points.Length < 3 || shape.Points.Length % 2 == 0))
        {
            return false;
        }

        if (shape.Type == SqlSpatialShapeType.Polygon)
        {
            return shape.Children.All(predicate: ring => ring.Points.Length >= 4 && ring.Points[0] == ring.Points[^1]);
        }

        return shape.Children.All(predicate: IsValid);
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) Bounds(SqlSpatialShape shape)
    {
        var points = Coordinates(shape: shape)
            .ToArray();

        if (points.Length == 0)
        {
            return (double.NaN, double.NaN, double.NaN, double.NaN);
        }

        return (points.Min(selector: point => point.X), points.Min(selector: point => point.Y), points.Max(selector: point => point.X), points.Max(selector: point => point.Y));
    }

    private static SqlSpatialShape Envelope(SqlSpatialShape shape)
    {
        var box = Bounds(shape: shape);

        if (double.IsNaN(d: box.MinX))
        {
            return CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = null, ChildSource = null, IsEmpty = true, Type = SqlSpatialShapeType.Polygon });
        }

        var ring = new[]
        {
            new sql_storage_engine.Models.Spatial.SqlSpatialCoordinate
            {
                Z = null,
                M = null,
                X = box.MinX,
                Y = box.MinY
            },
            new sql_storage_engine.Models.Spatial.SqlSpatialCoordinate
            {
                Z = null,
                M = null,
                X = box.MaxX,
                Y = box.MinY
            },
            new sql_storage_engine.Models.Spatial.SqlSpatialCoordinate
            {
                Z = null,
                M = null,
                X = box.MaxX,
                Y = box.MaxY
            },
            new sql_storage_engine.Models.Spatial.SqlSpatialCoordinate
            {
                Z = null,
                M = null,
                X = box.MinX,
                Y = box.MaxY
            },
            new sql_storage_engine.Models.Spatial.SqlSpatialCoordinate
            {
                Z = null,
                M = null,
                X = box.MinX,
                Y = box.MinY
            }
        };

        return CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = null, ChildSource = [CreateSqlSpatialShape(sqlSpatialShape: new sql_storage_engine.Models.Spatial.SqlSpatialShape { PointSource = ring, ChildSource = null, IsEmpty = false, Type = SqlSpatialShapeType.LineString })], IsEmpty = false, Type = SqlSpatialShapeType.Polygon });
    }

    private static double Length(SqlSpatialShape shape, SqlSpatialKind kind) =>
        Segments(shape: shape)
            .Sum(selector: segment => SegmentLength(a: segment.A, b: segment.B, kind: kind));

    private static double Area(SqlSpatialShape shape, SqlSpatialKind kind)
    {
        if (shape.Type == SqlSpatialShapeType.FullGlobe)
        {
            return 4 * Math.PI * EarthRadius * EarthRadius;
        }

        return Polygons(shape: shape)
            .Sum(selector: polygon => Math.Abs(value: polygon.Children.Select(selector: (ring, index) => (index == 0 ? 1 : -1) * RingArea(ring: ring.Points, kind: kind))
            .Sum()));
    }

    private static bool EqualsTopologically(SqlSpatialShape left, SqlSpatialShape right)
    {
        if (left.Type != right.Type || left.IsEmpty != right.IsEmpty)
        {
            return false;
        }

        if (left.IsEmpty)
        {
            return true;
        }

        if (left.Type == SqlSpatialShapeType.Point)
        {
            return SamePoint(left: left.Points.Single(), right: right.Points.Single());
        }

        if (left.Type is SqlSpatialShapeType.LineString or SqlSpatialShapeType.CircularString)
        {
            return SameLine(left: left.Points, right: right.Points);
        }

        if (left.Type == SqlSpatialShapeType.Polygon)
        {
            return SamePolygon(left: left, right: right);
        }

        if (left.Type == SqlSpatialShapeType.FullGlobe)
        {
            return true;
        }

        return SameUnorderedChildren(left: left.Children, right: right.Children);
    }

    private static bool Intersects(SqlSpatialShape left, SqlSpatialShape right)
    {
        if (left.IsEmpty || right.IsEmpty)
        {
            return false;
        }

        if (left.Type == SqlSpatialShapeType.FullGlobe || right.Type == SqlSpatialShapeType.FullGlobe)
        {
            return true;
        }

        if (!OverlapBounds(first: Bounds(shape: left), second: Bounds(shape: right)))
        {
            return false;
        }

        foreach (var a in Segments(shape: left))
        {
            foreach (var b in Segments(shape: right))
            {
                if (SegmentsIntersect(a: a.A, b: a.B, c: b.A, d: b.B))
                {
                    return true;
                }
            }
        }

        var leftPoint = Coordinates(shape: left)
            .FirstOrDefault();

        var rightPoint = Coordinates(shape: right)
            .FirstOrDefault();

        return ContainsPoint(shape: left, point: rightPoint) || ContainsPoint(shape: right, point: leftPoint);
    }

    private static bool Contains(SqlSpatialShape container, SqlSpatialShape candidate)
    {
        if (container.IsEmpty || candidate.IsEmpty)
        {
            return false;
        }

        if (container.Type == SqlSpatialShapeType.FullGlobe)
        {
            return true;
        }

        if (candidate.Type == SqlSpatialShapeType.FullGlobe)
        {
            return false;
        }

        if (!ContainBounds(first: Bounds(shape: container), second: Bounds(shape: candidate)))
        {
            return false;
        }

        return Coordinates(shape: candidate)
            .All(predicate: point => ContainsPoint(shape: container, point: point));
    }

    private static double Distance(SqlSpatialShape left, SqlSpatialShape right, SqlSpatialKind kind)
    {
        if (left.IsEmpty || right.IsEmpty)
        {
            return double.NaN;
        }

        if (Intersects(left: left, right: right))
        {
            return 0;
        }

        var leftPoints = Coordinates(shape: left)
            .ToArray();

        var rightPoints = Coordinates(shape: right)
            .ToArray();

        var distances = new List<double>();

        foreach (var point in leftPoints)
        {
            foreach (var segment in Segments(shape: right))
            {
                distances.Add(item: kind == SqlSpatialKind.Geography ? GeographyPointSegmentDistance(point: point, start: segment.A, end: segment.B) : PointSegmentDistance(p: point, a: segment.A, b: segment.B));
            }
        }

        foreach (var point in rightPoints)
        {
            foreach (var segment in Segments(shape: left))
            {
                distances.Add(item: kind == SqlSpatialKind.Geography ? GeographyPointSegmentDistance(point: point, start: segment.A, end: segment.B) : PointSegmentDistance(p: point, a: segment.A, b: segment.B));
            }
        }

        if (distances.Count == 0)
        {
            distances.AddRange(collection: leftPoints.SelectMany(selector: a => rightPoints.Select(selector: b => SegmentLength(a: a, b: b, kind: kind))));
        }

        return distances.DefaultIfEmpty(defaultValue: double.NaN)
            .Min();
    }

    private static bool SamePoint(SqlSpatialCoordinate left, SqlSpatialCoordinate right) =>
        left.X == right.X && left.Y == right.Y;

    private static bool SameLine(IReadOnlyList<SqlSpatialCoordinate> left, IReadOnlyList<SqlSpatialCoordinate> right)
    {
        var a = Simplify(points: left, closed: false);
        var b = Simplify(points: right, closed: false);

        return SameSequence(left: a, right: b) || SameSequence(left: a, right: b.AsEnumerable()
            .Reverse()
            .ToArray());
    }

    private static bool SamePolygon(SqlSpatialShape left, SqlSpatialShape right)
    {
        if (left.Children.Length != right.Children.Length || left.Children.Length == 0)
        {
            return false;
        }

        if (!SameRing(left: left.Children[0].Points, right: right.Children[0].Points))
        {
            return false;
        }

        var unmatched = right.Children.Skip(count: 1)
            .ToList();

        foreach (var ring in left.Children.Skip(count: 1))
        {
            var match = unmatched.FindIndex(match: candidate => SameRing(left: ring.Points, right: candidate.Points));

            if (match < 0)
            {
                return false;
            }

            unmatched.RemoveAt(index: match);
        }

        return unmatched.Count == 0;
    }

    private static bool SameRing(IReadOnlyList<SqlSpatialCoordinate> left, IReadOnlyList<SqlSpatialCoordinate> right)
    {
        var a = Simplify(points: left, closed: true);
        var b = Simplify(points: right, closed: true);

        if (a.Length != b.Length)
        {
            return false;
        }

        for (var start = 0; start < b.Length; start++)
        {
            var forward = true;
            var reverse = true;

            for (var offset = 0; offset < a.Length && (forward || reverse); offset++)
            {
                forward &= SamePoint(left: a[offset], right: b[(start + offset) % b.Length]);
                reverse &= SamePoint(left: a[offset], right: b[(start - offset + b.Length) % b.Length]);
            }

            if (forward || reverse)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameUnorderedChildren(IReadOnlyList<SqlSpatialShape> left, IReadOnlyList<SqlSpatialShape> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        var unmatched = right.ToList();

        foreach (var child in left)
        {
            var match = unmatched.FindIndex(match: candidate => EqualsTopologically(left: child, right: candidate));

            if (match < 0)
            {
                return false;
            }

            unmatched.RemoveAt(index: match);
        }

        return true;
    }

    private static SqlSpatialCoordinate[] Simplify(IReadOnlyList<SqlSpatialCoordinate> points, bool closed)
    {
        var result = points.Where(predicate: (point, index) => index == 0 || !SamePoint(left: point, right: points[index - 1]))
            .ToList();

        if (closed && result.Count > 1 && SamePoint(left: result[0], right: result[^1]))
        {
            result.RemoveAt(index: result.Count - 1);
        }

        var changed = true;

        while (changed && result.Count > (closed ? 3 : 2))
        {
            changed = false;

            for (var index = closed ? 0 : 1; index < result.Count - (closed ? 0 : 1); index++)
            {
                var previous = result[(index - 1 + result.Count) % result.Count];
                var current = result[index];
                var next = result[(index + 1) % result.Count];

                if (!CollinearBetween(a: previous, b: current, c: next))
                {
                    continue;
                }

                result.RemoveAt(index: index);
                changed = true;
                break;
            }
        }

        return result.ToArray();
    }

    private static bool SameSequence(IReadOnlyList<SqlSpatialCoordinate> left, IReadOnlyList<SqlSpatialCoordinate> right) =>
        left.Count == right.Count && left.Select(selector: (point, index) => SamePoint(left: point, right: right[index]))
            .All(predicate: equal => equal);

    private static bool CollinearBetween(SqlSpatialCoordinate a, SqlSpatialCoordinate b, SqlSpatialCoordinate c)
    {
        var cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        if (Math.Abs(value: cross) > 1e-12)
        {
            return false;
        }

        return b.X >= Math.Min(val1: a.X, val2: c.X) && b.X <= Math.Max(val1: a.X, val2: c.X) && b.Y >= Math.Min(val1: a.Y, val2: c.Y) && b.Y <= Math.Max(val1: a.Y, val2: c.Y);
    }

    private static IEnumerable<SqlSpatialShape> Polygons(SqlSpatialShape shape) =>
        shape.Type switch
    {
        SqlSpatialShapeType.Polygon => [shape],
        _ => shape.Children.SelectMany(selector: Polygons)};

    private static IEnumerable<(SqlSpatialCoordinate A, SqlSpatialCoordinate B)> Segments(SqlSpatialShape shape)
    {
        if (shape.Type is SqlSpatialShapeType.LineString or SqlSpatialShapeType.CircularString)
        {
            for (var index = 1; index < shape.Points.Length; index++)
            {
                yield return (shape.Points[index - 1], shape.Points[index]);
            }
        }

        foreach (var child in shape.Children)
        {
            foreach (var segment in Segments(shape: child))
            {
                yield return segment;
            }
        }
    }

    private static bool ContainsPoint(SqlSpatialShape shape, SqlSpatialCoordinate point)
    {
        foreach (var polygon in Polygons(shape: shape))
        {
            if (!PointInRing(ring: polygon.Children[0].Points, point: point))
            {
                continue;
            }

            if (polygon.Children.Skip(count: 1)
            .Any(predicate: hole => PointInRing(ring: hole.Points, point: point)))
            {
                continue;
            }

            return true;
        }

        return Coordinates(shape: shape)
            .Any(predicate: candidate => candidate.X == point.X && candidate.Y == point.Y);
    }

    private static bool PointInRing(IReadOnlyList<SqlSpatialCoordinate> ring, SqlSpatialCoordinate point)
    {
        var inside = false;

        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var a = ring[i];
            var b = ring[j];

            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool SegmentsIntersect(SqlSpatialCoordinate a, SqlSpatialCoordinate b, SqlSpatialCoordinate c, SqlSpatialCoordinate d)
    {
        static double Cross(SqlSpatialCoordinate p, SqlSpatialCoordinate q, SqlSpatialCoordinate r) => (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        static bool OnSegment(SqlSpatialCoordinate p, SqlSpatialCoordinate q, SqlSpatialCoordinate r) => q.X >= Math.Min(val1: p.X, val2: r.X) && q.X <= Math.Max(val1: p.X, val2: r.X) && q.Y >= Math.Min(val1: p.Y, val2: r.Y) && q.Y <= Math.Max(val1: p.Y, val2: r.Y);
        var abC = Cross(p: a, q: b, r: c);
        var abD = Cross(p: a, q: b, r: d);
        var cdA = Cross(p: c, q: d, r: a);
        var cdB = Cross(p: c, q: d, r: b);

        if (Math.Sign(value: abC) != Math.Sign(value: abD) && Math.Sign(value: cdA) != Math.Sign(value: cdB))
        {
            return true;
        }

        const double epsilon = 1e-12;
        return Math.Abs(value: abC) <= epsilon && OnSegment(p: a, q: c, r: b) || Math.Abs(value: abD) <= epsilon && OnSegment(p: a, q: d, r: b) || Math.Abs(value: cdA) <= epsilon && OnSegment(p: c, q: a, r: d) || Math.Abs(value: cdB) <= epsilon && OnSegment(p: c, q: b, r: d);
    }

    private static double PointSegmentDistance(SqlSpatialCoordinate p, SqlSpatialCoordinate a, SqlSpatialCoordinate b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;

        if (dx == 0 && dy == 0)
        {
            return Math.Sqrt(d: Math.Pow(x: p.X - a.X, y: 2) + Math.Pow(x: p.Y - a.Y, y: 2));
        }

        var t = Math.Clamp(value: ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy), min: 0, max: 1);
        return Math.Sqrt(d: Math.Pow(x: p.X - (a.X + t * dx), y: 2) + Math.Pow(x: p.Y - (a.Y + t * dy), y: 2));
    }

    private static double GeographyPointSegmentDistance(SqlSpatialCoordinate point, SqlSpatialCoordinate start, SqlSpatialCoordinate end)
    {
        var segmentLength = AngularDistance(a: start, b: end);

        if (segmentLength <= double.Epsilon)
        {
            return SegmentLength(a: point, b: start, kind: SqlSpatialKind.Geography);
        }

        var pointDistance = AngularDistance(a: start, b: point);
        var crossTrack = Math.Asin(d: Math.Clamp(value: Math.Sin(a: pointDistance) * Math.Sin(a: InitialBearing(from: start, to: point) - InitialBearing(from: start, to: end)), min: -1, max: 1));
        var bearingDelta = InitialBearing(from: start, to: point) - InitialBearing(from: start, to: end);
        var alongTrack = Math.Atan2(y: Math.Sin(a: pointDistance) * Math.Cos(d: bearingDelta), x: Math.Cos(d: pointDistance));

        if (double.IsFinite(d: alongTrack) && alongTrack >= 0 && alongTrack <= segmentLength)
        {
            return Math.Abs(value: crossTrack) * EarthRadius;
        }

        return Math.Min(val1: SegmentLength(a: point, b: start, kind: SqlSpatialKind.Geography), val2: SegmentLength(a: point, b: end, kind: SqlSpatialKind.Geography));
    }

    private static double AngularDistance(SqlSpatialCoordinate a, SqlSpatialCoordinate b) =>
        SegmentLength(a: a, b: b, kind: SqlSpatialKind.Geography) / EarthRadius;

    private static double InitialBearing(SqlSpatialCoordinate from, SqlSpatialCoordinate to)
    {
        var latitude1 = Degrees(value: from.Y);
        var latitude2 = Degrees(value: to.Y);
        var longitude = Degrees(value: to.X - from.X);
        return Math.Atan2(y: Math.Sin(a: longitude) * Math.Cos(d: latitude2), x: Math.Cos(d: latitude1) * Math.Sin(a: latitude2) - Math.Sin(a: latitude1) * Math.Cos(d: latitude2) * Math.Cos(d: longitude));
    }

    private static double SegmentLength(SqlSpatialCoordinate a, SqlSpatialCoordinate b, SqlSpatialKind kind)
    {
        if (kind == SqlSpatialKind.Geometry)
        {
            return Math.Sqrt(d: Math.Pow(x: a.X - b.X, y: 2) + Math.Pow(x: a.Y - b.Y, y: 2));
        }

        var lat1 = Degrees(value: a.Y);
        var lat2 = Degrees(value: b.Y);
        var dlat = lat2 - lat1;
        var dlon = Degrees(value: b.X - a.X);
        var h = Math.Pow(x: Math.Sin(a: dlat / 2), y: 2) + Math.Cos(d: lat1) * Math.Cos(d: lat2) * Math.Pow(x: Math.Sin(a: dlon / 2), y: 2);
        return 2 * EarthRadius * Math.Asin(d: Math.Min(val1: 1, val2: Math.Sqrt(d: h)));
    }

    private static double RingArea(IReadOnlyList<SqlSpatialCoordinate> ring, SqlSpatialKind kind)
    {
        if (kind == SqlSpatialKind.Geography)
        {
            double spherical = 0;

            for (var index = 1; index < ring.Count; index++)
            {
                var longitude = Degrees(value: ring[index].X - ring[index - 1].X);

                if (longitude > Math.PI)
                {
                    longitude -= 2 * Math.PI;
                }
                else if (longitude < -Math.PI)
                {
                    longitude += 2 * Math.PI;
                }

                spherical += longitude * (2 + Math.Sin(a: Degrees(value: ring[index - 1].Y)) + Math.Sin(a: Degrees(value: ring[index].Y)));
            }

            return Math.Abs(value: spherical) * EarthRadius * EarthRadius / 2;
        }

        double area = 0;

        for (var index = 1; index < ring.Count; index++)
        {
            area += ring[index - 1].X * ring[index].Y - ring[index].X * ring[index - 1].Y;
        }

        area = Math.Abs(value: area / 2);
        return area;
    }

    private static double Degrees(double value) =>
        value * Math.PI / 180d;
}