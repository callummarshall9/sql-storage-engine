using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Rows;

internal enum SqlSpatialShapeType : byte
{
    Point = 1, LineString, Polygon, MultiPoint, MultiLineString, MultiPolygon, GeometryCollection,
    CircularString, CompoundCurve, CurvePolygon, FullGlobe
}

internal static class SqlSpatialShapeTypeExtensions
{
    public static string ToSqlName(this SqlSpatialShapeType type) => type switch
    {
        SqlSpatialShapeType.LineString => "LineString", SqlSpatialShapeType.MultiPoint => "MultiPoint",
        SqlSpatialShapeType.MultiLineString => "MultiLineString", SqlSpatialShapeType.MultiPolygon => "MultiPolygon",
        SqlSpatialShapeType.GeometryCollection => "GeometryCollection", SqlSpatialShapeType.CircularString => "CircularString",
        SqlSpatialShapeType.CompoundCurve => "CompoundCurve", SqlSpatialShapeType.CurvePolygon => "CurvePolygon",
        SqlSpatialShapeType.FullGlobe => "FullGlobe", _ => type.ToString()
    };
}

internal readonly record struct SqlSpatialCoordinate(double X, double Y, double? Z = null, double? M = null)
{
    public string ToWkt()
    {
        var values = new List<string> { X.ToString("R", CultureInfo.InvariantCulture), Y.ToString("R", CultureInfo.InvariantCulture) };
        if (Z is { } z) values.Add(z.ToString("R", CultureInfo.InvariantCulture));
        if (M is { } m) values.Add(m.ToString("R", CultureInfo.InvariantCulture));
        return string.Join(' ', values);
    }
}

internal sealed class SqlSpatialShape
{
    public SqlSpatialShape(SqlSpatialShapeType type, IEnumerable<SqlSpatialCoordinate>? points = null,
        IEnumerable<SqlSpatialShape>? children = null, bool isEmpty = false)
    {
        Type = type; Points = points?.ToArray() ?? []; Children = children?.ToArray() ?? []; IsEmpty = isEmpty;
    }
    public SqlSpatialShapeType Type { get; }
    public SqlSpatialCoordinate[] Points { get; }
    public SqlSpatialShape[] Children { get; }
    public bool IsEmpty { get; }
    public int Dimension => Type switch
    {
        SqlSpatialShapeType.Point or SqlSpatialShapeType.MultiPoint => 0,
        SqlSpatialShapeType.LineString or SqlSpatialShapeType.MultiLineString or SqlSpatialShapeType.CircularString or
            SqlSpatialShapeType.CompoundCurve => 1,
        SqlSpatialShapeType.Polygon or SqlSpatialShapeType.MultiPolygon or SqlSpatialShapeType.CurvePolygon or
            SqlSpatialShapeType.FullGlobe => 2,
        SqlSpatialShapeType.GeometryCollection => Children.Length == 0 ? -1 : Children.Max(child => child.Dimension),
        _ => -1
    };
    public IEnumerable<SqlSpatialCoordinate> Coordinates() => Points.Concat(Children.SelectMany(child => child.Coordinates()));
    public static SqlSpatialShape Point(SqlSpatialCoordinate point) => new(SqlSpatialShapeType.Point, [point]);

    public string ToWkt()
    {
        var name = Type.ToSqlName().ToUpperInvariant();
        if (Type == SqlSpatialShapeType.FullGlobe) return name;
        if (IsEmpty) return $"{name} EMPTY";
        return Type switch
        {
            SqlSpatialShapeType.Point => $"{name} ({Points.Single().ToWkt()})",
            SqlSpatialShapeType.LineString or SqlSpatialShapeType.CircularString => $"{name} ({CoordinatesText(Points)})",
            SqlSpatialShapeType.Polygon => $"{name} ({string.Join(',', Children.Select(RingText))})",
            SqlSpatialShapeType.MultiPoint => $"{name} ({string.Join(',', Children.Select(child => $"({child.Points.Single().ToWkt()})"))})",
            SqlSpatialShapeType.MultiLineString => $"{name} ({string.Join(',', Children.Select(RingText))})",
            SqlSpatialShapeType.MultiPolygon => $"{name} ({string.Join(',', Children.Select(PolygonBody))})",
            SqlSpatialShapeType.GeometryCollection => $"{name} ({string.Join(',', Children.Select(child => child.ToWkt()))})",
            SqlSpatialShapeType.CompoundCurve or SqlSpatialShapeType.CurvePolygon =>
                $"{name} ({string.Join(',', Children.Select(child => child.Type == SqlSpatialShapeType.LineString ? RingText(child) : child.ToWkt()))})",
            _ => throw new InvalidOperationException("Unknown spatial shape.")
        };
    }
    private static string CoordinatesText(IEnumerable<SqlSpatialCoordinate> points) => string.Join(',', points.Select(point => point.ToWkt()));
    private static string RingText(SqlSpatialShape line) => $"({CoordinatesText(line.Points)})";
    private static string PolygonBody(SqlSpatialShape polygon) => $"({string.Join(',', polygon.Children.Select(RingText))})";
}

internal ref struct SqlSpatialParser
{
    private readonly ReadOnlySpan<char> _source; private int _position;
    private SqlSpatialParser(string source) { _source = source.AsSpan(); _position = 0; }
    public static SqlSpatialShape Parse(string source)
    {
        var parser = new SqlSpatialParser(source); var shape = parser.Shape(); parser.White();
        if (!parser.End) throw new FormatException("Spatial WKT contains trailing input.");
        return shape;
    }
    private bool End => _position == _source.Length;
    private SqlSpatialShape Shape()
    {
        var type = Type(Word()); White();
        var dimension = Dimension.None;
        var saved = _position;
        if (!End && char.IsLetter(_source[_position]))
        {
            dimension = Word().ToUpperInvariant() switch { "Z" => Dimension.Z, "M" => Dimension.M, "ZM" => Dimension.ZM, _ => Dimension.None };
            if (dimension == Dimension.None) _position = saved;
            White();
        }
        if (TryWord("EMPTY")) return new SqlSpatialShape(type, isEmpty: true);
        if (type == SqlSpatialShapeType.FullGlobe) return new SqlSpatialShape(type);
        return type switch
        {
            SqlSpatialShapeType.Point => new(type, [PointBody(dimension)]),
            SqlSpatialShapeType.LineString or SqlSpatialShapeType.CircularString => new(type, PointListBody(dimension)),
            SqlSpatialShapeType.Polygon => PolygonBody(type, dimension),
            SqlSpatialShapeType.MultiPoint => MultiPointBody(dimension),
            SqlSpatialShapeType.MultiLineString => MultiLineBody(dimension),
            SqlSpatialShapeType.MultiPolygon => MultiPolygonBody(dimension),
            SqlSpatialShapeType.GeometryCollection => CollectionBody(type),
            SqlSpatialShapeType.CompoundCurve or SqlSpatialShapeType.CurvePolygon => CurveBody(type, dimension),
            _ => throw new FormatException("Unsupported spatial type.")
        };
    }
    private SqlSpatialCoordinate PointBody(Dimension dimension) { Open(); var point = Coordinate(dimension); Close(); return point; }
    private SqlSpatialCoordinate[] PointListBody(Dimension dimension) { Open(); var points = PointList(dimension); Close(); return points; }
    private SqlSpatialShape PolygonBody(SqlSpatialShapeType type, Dimension dimension)
    {
        Open(); var rings = new List<SqlSpatialShape>();
        do { Open(); var points = PointList(dimension); Close(); rings.Add(new SqlSpatialShape(SqlSpatialShapeType.LineString, points)); }
        while (Comma()); Close(); return new SqlSpatialShape(type, children: rings);
    }
    private SqlSpatialShape MultiPointBody(Dimension dimension)
    {
        Open(); var points = new List<SqlSpatialShape>();
        do
        {
            White(); var wrapped = !End && _source[_position] == '('; if (wrapped) Open();
            points.Add(SqlSpatialShape.Point(Coordinate(dimension))); if (wrapped) Close();
        } while (Comma()); Close(); return new SqlSpatialShape(SqlSpatialShapeType.MultiPoint, children: points);
    }
    private SqlSpatialShape MultiLineBody(Dimension dimension)
    {
        Open(); var lines = new List<SqlSpatialShape>();
        do { Open(); lines.Add(new SqlSpatialShape(SqlSpatialShapeType.LineString, PointList(dimension))); Close(); }
        while (Comma()); Close(); return new SqlSpatialShape(SqlSpatialShapeType.MultiLineString, children: lines);
    }
    private SqlSpatialShape MultiPolygonBody(Dimension dimension)
    {
        Open(); var polygons = new List<SqlSpatialShape>();
        do { polygons.Add(PolygonBody(SqlSpatialShapeType.Polygon, dimension)); } while (Comma());
        Close(); return new SqlSpatialShape(SqlSpatialShapeType.MultiPolygon, children: polygons);
    }
    private SqlSpatialShape CollectionBody(SqlSpatialShapeType type)
    {
        Open(); var children = new List<SqlSpatialShape>(); do { children.Add(Shape()); } while (Comma()); Close();
        return new SqlSpatialShape(type, children: children);
    }
    private SqlSpatialShape CurveBody(SqlSpatialShapeType type, Dimension dimension)
    {
        Open(); var children = new List<SqlSpatialShape>();
        do
        {
            White();
            if (_source[_position] == '(') children.Add(new SqlSpatialShape(SqlSpatialShapeType.LineString, PointListBody(dimension)));
            else children.Add(Shape());
        } while (Comma()); Close(); return new SqlSpatialShape(type, children: children);
    }
    private SqlSpatialCoordinate[] PointList(Dimension dimension)
    { var points = new List<SqlSpatialCoordinate> { Coordinate(dimension) }; while (Comma()) points.Add(Coordinate(dimension)); return points.ToArray(); }
    private SqlSpatialCoordinate Coordinate(Dimension dimension)
    {
        var values = new List<double> { Number(), Number() };
        var expected = dimension switch { Dimension.Z or Dimension.M => 3, Dimension.ZM => 4, _ => 0 };
        if (expected == 0)
        {
            while (values.Count < 4 && HasNumber()) values.Add(Number());
            expected = values.Count;
        }
        else while (values.Count < expected) values.Add(Number());
        return dimension switch
        {
            Dimension.M => new(values[0], values[1], null, values[2]),
            Dimension.ZM => new(values[0], values[1], values[2], values[3]),
            _ => new(values[0], values[1], values.Count > 2 ? values[2] : null, values.Count > 3 ? values[3] : null)
        };
    }
    private bool HasNumber() { White(); return !End && _source[_position] is not (',' or ')'); }
    private double Number()
    {
        White(); var start = _position;
        while (!End && !char.IsWhiteSpace(_source[_position]) && _source[_position] is not (',' or ')')) _position++;
        if (start == _position || !double.TryParse(_source[start.._position], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new FormatException("Spatial coordinate is invalid.");
        White(); return value;
    }
    private string Word()
    { White(); var start = _position; while (!End && char.IsLetter(_source[_position])) _position++; if (start == _position) throw new FormatException("Expected a spatial type."); return _source[start.._position].ToString(); }
    private bool TryWord(string expected)
    { var saved = _position; if (End || !char.IsLetter(_source[_position])) return false; var word = Word(); if (word.Equals(expected, StringComparison.OrdinalIgnoreCase)) return true; _position = saved; return false; }
    private void Open() { White(); Expect('('); White(); }
    private void Close() { White(); Expect(')'); White(); }
    private bool Comma() { White(); if (End || _source[_position] != ',') return false; _position++; White(); return true; }
    private void Expect(char value) { if (End || _source[_position++] != value) throw new FormatException($"Expected '{value}' in spatial WKT."); }
    private void White() { while (!End && char.IsWhiteSpace(_source[_position])) _position++; }
    private static SqlSpatialShapeType Type(string word) => word.ToUpperInvariant() switch
    {
        "POINT" => SqlSpatialShapeType.Point, "LINESTRING" => SqlSpatialShapeType.LineString,
        "POLYGON" => SqlSpatialShapeType.Polygon, "MULTIPOINT" => SqlSpatialShapeType.MultiPoint,
        "MULTILINESTRING" => SqlSpatialShapeType.MultiLineString, "MULTIPOLYGON" => SqlSpatialShapeType.MultiPolygon,
        "GEOMETRYCOLLECTION" => SqlSpatialShapeType.GeometryCollection, "CIRCULARSTRING" => SqlSpatialShapeType.CircularString,
        "COMPOUNDCURVE" => SqlSpatialShapeType.CompoundCurve, "CURVEPOLYGON" => SqlSpatialShapeType.CurvePolygon,
        "FULLGLOBE" => SqlSpatialShapeType.FullGlobe, _ => throw new FormatException($"Unknown spatial type '{word}'.")
    };
    private enum Dimension : byte { None, Z, M, ZM }
}

internal readonly record struct SqlSpatialEnvelope(double MinX, double MinY, double MaxX, double MaxY)
{
    public bool Intersects(SqlSpatialEnvelope other) => MinX <= other.MaxX && MaxX >= other.MinX && MinY <= other.MaxY && MaxY >= other.MinY;
    public bool Contains(SqlSpatialEnvelope other) => MinX <= other.MinX && MinY <= other.MinY && MaxX >= other.MaxX && MaxY >= other.MaxY;
}

internal static class SqlSpatialOperations
{
    private const double EarthRadius = 6_371_008.8;
    public static void ValidateGeography(SqlSpatialShape shape)
    {
        foreach (var point in shape.Coordinates())
            if (point.X is < -180 or > 180 || point.Y is < -90 or > 90)
                throw new ArgumentOutOfRangeException(nameof(shape), "Geography longitude/latitude is outside its valid range.");
    }
    public static bool IsValid(SqlSpatialShape shape)
    {
        if (shape.IsEmpty || shape.Type == SqlSpatialShapeType.FullGlobe) return true;
        if (shape.Type is SqlSpatialShapeType.LineString && shape.Points.Length < 2) return false;
        if (shape.Type is SqlSpatialShapeType.CircularString && (shape.Points.Length < 3 || shape.Points.Length % 2 == 0)) return false;
        if (shape.Type == SqlSpatialShapeType.Polygon)
            return shape.Children.All(ring => ring.Points.Length >= 4 && ring.Points[0] == ring.Points[^1]);
        return shape.Children.All(IsValid);
    }
    public static SqlSpatialEnvelope Bounds(SqlSpatialShape shape)
    {
        var points = shape.Coordinates().ToArray();
        if (points.Length == 0) return new(double.NaN, double.NaN, double.NaN, double.NaN);
        return new(points.Min(point => point.X), points.Min(point => point.Y), points.Max(point => point.X), points.Max(point => point.Y));
    }
    public static SqlSpatialShape Envelope(SqlSpatialShape shape)
    {
        var box = Bounds(shape); if (double.IsNaN(box.MinX)) return new SqlSpatialShape(SqlSpatialShapeType.Polygon, isEmpty: true);
        var ring = new[] { new SqlSpatialCoordinate(box.MinX, box.MinY), new(box.MaxX, box.MinY),
            new SqlSpatialCoordinate(box.MaxX, box.MaxY), new(box.MinX, box.MaxY), new(box.MinX, box.MinY) };
        return new SqlSpatialShape(SqlSpatialShapeType.Polygon, children: [new SqlSpatialShape(SqlSpatialShapeType.LineString, ring)]);
    }
    public static double Length(SqlSpatialShape shape, SqlSpatialKind kind) => Segments(shape).Sum(segment => SegmentLength(segment.A, segment.B, kind));
    public static double Area(SqlSpatialShape shape, SqlSpatialKind kind)
    {
        if (shape.Type == SqlSpatialShapeType.FullGlobe) return 4 * Math.PI * EarthRadius * EarthRadius;
        return Polygons(shape).Sum(polygon => Math.Abs(polygon.Children.Select((ring, index) => (index == 0 ? 1 : -1) * RingArea(ring.Points, kind)).Sum()));
    }
    public static bool EqualsTopologically(SqlSpatialShape left, SqlSpatialShape right)
    {
        if (left.Type != right.Type || left.IsEmpty != right.IsEmpty) return false;
        if (left.IsEmpty) return true;
        if (left.Type == SqlSpatialShapeType.Point) return SamePoint(left.Points.Single(), right.Points.Single());
        if (left.Type is SqlSpatialShapeType.LineString or SqlSpatialShapeType.CircularString)
            return SameLine(left.Points, right.Points);
        if (left.Type == SqlSpatialShapeType.Polygon)
            return SamePolygon(left, right);
        if (left.Type == SqlSpatialShapeType.FullGlobe) return true;
        return SameUnorderedChildren(left.Children, right.Children);
    }
    public static bool Intersects(SqlSpatialShape left, SqlSpatialShape right)
    {
        if (left.IsEmpty || right.IsEmpty) return false;
        if (left.Type == SqlSpatialShapeType.FullGlobe || right.Type == SqlSpatialShapeType.FullGlobe) return true;
        if (!Bounds(left).Intersects(Bounds(right))) return false;
        foreach (var a in Segments(left)) foreach (var b in Segments(right)) if (SegmentsIntersect(a.A, a.B, b.A, b.B)) return true;
        var leftPoint = left.Coordinates().FirstOrDefault(); var rightPoint = right.Coordinates().FirstOrDefault();
        return ContainsPoint(left, rightPoint) || ContainsPoint(right, leftPoint);
    }
    public static bool Contains(SqlSpatialShape container, SqlSpatialShape candidate)
    {
        if (container.IsEmpty || candidate.IsEmpty) return false;
        if (container.Type == SqlSpatialShapeType.FullGlobe) return true;
        if (candidate.Type == SqlSpatialShapeType.FullGlobe) return false;
        if (!Bounds(container).Contains(Bounds(candidate))) return false;
        return candidate.Coordinates().All(point => ContainsPoint(container, point));
    }
    public static double Distance(SqlSpatialShape left, SqlSpatialShape right, SqlSpatialKind kind)
    {
        if (left.IsEmpty || right.IsEmpty) return double.NaN;
        if (Intersects(left, right)) return 0;
        var leftPoints = left.Coordinates().ToArray(); var rightPoints = right.Coordinates().ToArray();
        var distances = new List<double>();
        foreach (var point in leftPoints) foreach (var segment in Segments(right))
            distances.Add(kind == SqlSpatialKind.Geography ? GeographyPointSegmentDistance(point, segment.A, segment.B) : PointSegmentDistance(point, segment.A, segment.B));
        foreach (var point in rightPoints) foreach (var segment in Segments(left))
            distances.Add(kind == SqlSpatialKind.Geography ? GeographyPointSegmentDistance(point, segment.A, segment.B) : PointSegmentDistance(point, segment.A, segment.B));
        if (distances.Count == 0) distances.AddRange(leftPoints.SelectMany(a => rightPoints.Select(b => SegmentLength(a, b, kind))));
        return distances.DefaultIfEmpty(double.NaN).Min();
    }
    private static bool SamePoint(SqlSpatialCoordinate left, SqlSpatialCoordinate right) => left.X == right.X && left.Y == right.Y;
    private static bool SameLine(IReadOnlyList<SqlSpatialCoordinate> left, IReadOnlyList<SqlSpatialCoordinate> right)
    {
        var a = Simplify(left, closed: false); var b = Simplify(right, closed: false);
        return SameSequence(a, b) || SameSequence(a, b.AsEnumerable().Reverse().ToArray());
    }
    private static bool SamePolygon(SqlSpatialShape left, SqlSpatialShape right)
    {
        if (left.Children.Length != right.Children.Length || left.Children.Length == 0) return false;
        if (!SameRing(left.Children[0].Points, right.Children[0].Points)) return false;
        var unmatched = right.Children.Skip(1).ToList();
        foreach (var ring in left.Children.Skip(1))
        {
            var match = unmatched.FindIndex(candidate => SameRing(ring.Points, candidate.Points));
            if (match < 0) return false;
            unmatched.RemoveAt(match);
        }
        return unmatched.Count == 0;
    }
    private static bool SameRing(IReadOnlyList<SqlSpatialCoordinate> left, IReadOnlyList<SqlSpatialCoordinate> right)
    {
        var a = Simplify(left, closed: true); var b = Simplify(right, closed: true);
        if (a.Length != b.Length) return false;
        for (var start = 0; start < b.Length; start++)
        {
            var forward = true; var reverse = true;
            for (var offset = 0; offset < a.Length && (forward || reverse); offset++)
            {
                forward &= SamePoint(a[offset], b[(start + offset) % b.Length]);
                reverse &= SamePoint(a[offset], b[(start - offset + b.Length) % b.Length]);
            }
            if (forward || reverse) return true;
        }
        return false;
    }
    private static bool SameUnorderedChildren(IReadOnlyList<SqlSpatialShape> left, IReadOnlyList<SqlSpatialShape> right)
    {
        if (left.Count != right.Count) return false;
        var unmatched = right.ToList();
        foreach (var child in left)
        {
            var match = unmatched.FindIndex(candidate => EqualsTopologically(child, candidate));
            if (match < 0) return false;
            unmatched.RemoveAt(match);
        }
        return true;
    }
    private static SqlSpatialCoordinate[] Simplify(IReadOnlyList<SqlSpatialCoordinate> points, bool closed)
    {
        var result = points.Where((point, index) => index == 0 || !SamePoint(point, points[index - 1])).ToList();
        if (closed && result.Count > 1 && SamePoint(result[0], result[^1])) result.RemoveAt(result.Count - 1);
        var changed = true;
        while (changed && result.Count > (closed ? 3 : 2))
        {
            changed = false;
            for (var index = closed ? 0 : 1; index < result.Count - (closed ? 0 : 1); index++)
            {
                var previous = result[(index - 1 + result.Count) % result.Count];
                var current = result[index]; var next = result[(index + 1) % result.Count];
                if (!CollinearBetween(previous, current, next)) continue;
                result.RemoveAt(index); changed = true; break;
            }
        }
        return result.ToArray();
    }
    private static bool SameSequence(IReadOnlyList<SqlSpatialCoordinate> left, IReadOnlyList<SqlSpatialCoordinate> right) =>
        left.Count == right.Count && left.Select((point, index) => SamePoint(point, right[index])).All(equal => equal);
    private static bool CollinearBetween(SqlSpatialCoordinate a, SqlSpatialCoordinate b, SqlSpatialCoordinate c)
    {
        var cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (Math.Abs(cross) > 1e-12) return false;
        return b.X >= Math.Min(a.X, c.X) && b.X <= Math.Max(a.X, c.X) &&
               b.Y >= Math.Min(a.Y, c.Y) && b.Y <= Math.Max(a.Y, c.Y);
    }
    private static IEnumerable<SqlSpatialShape> Polygons(SqlSpatialShape shape) => shape.Type switch
    { SqlSpatialShapeType.Polygon => [shape], _ => shape.Children.SelectMany(Polygons) };
    private static IEnumerable<(SqlSpatialCoordinate A, SqlSpatialCoordinate B)> Segments(SqlSpatialShape shape)
    {
        if (shape.Type is SqlSpatialShapeType.LineString or SqlSpatialShapeType.CircularString)
            for (var index = 1; index < shape.Points.Length; index++) yield return (shape.Points[index - 1], shape.Points[index]);
        foreach (var child in shape.Children) foreach (var segment in Segments(child)) yield return segment;
    }
    private static bool ContainsPoint(SqlSpatialShape shape, SqlSpatialCoordinate point)
    {
        foreach (var polygon in Polygons(shape))
        {
            if (!PointInRing(polygon.Children[0].Points, point)) continue;
            if (polygon.Children.Skip(1).Any(hole => PointInRing(hole.Points, point))) continue;
            return true;
        }
        return shape.Coordinates().Any(candidate => candidate.X == point.X && candidate.Y == point.Y);
    }
    private static bool PointInRing(IReadOnlyList<SqlSpatialCoordinate> ring, SqlSpatialCoordinate point)
    {
        var inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            var a = ring[i]; var b = ring[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }
    private static bool SegmentsIntersect(SqlSpatialCoordinate a, SqlSpatialCoordinate b, SqlSpatialCoordinate c, SqlSpatialCoordinate d)
    {
        static double Cross(SqlSpatialCoordinate p, SqlSpatialCoordinate q, SqlSpatialCoordinate r) =>
            (q.X - p.X) * (r.Y - p.Y) - (q.Y - p.Y) * (r.X - p.X);
        static bool OnSegment(SqlSpatialCoordinate p, SqlSpatialCoordinate q, SqlSpatialCoordinate r) =>
            q.X >= Math.Min(p.X, r.X) && q.X <= Math.Max(p.X, r.X) &&
            q.Y >= Math.Min(p.Y, r.Y) && q.Y <= Math.Max(p.Y, r.Y);
        var abC = Cross(a, b, c); var abD = Cross(a, b, d); var cdA = Cross(c, d, a); var cdB = Cross(c, d, b);
        if (Math.Sign(abC) != Math.Sign(abD) && Math.Sign(cdA) != Math.Sign(cdB)) return true;
        const double epsilon = 1e-12;
        return Math.Abs(abC) <= epsilon && OnSegment(a, c, b) || Math.Abs(abD) <= epsilon && OnSegment(a, d, b) ||
            Math.Abs(cdA) <= epsilon && OnSegment(c, a, d) || Math.Abs(cdB) <= epsilon && OnSegment(c, b, d);
    }
    private static double PointSegmentDistance(SqlSpatialCoordinate p, SqlSpatialCoordinate a, SqlSpatialCoordinate b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        if (dx == 0 && dy == 0) return Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2));
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        return Math.Sqrt(Math.Pow(p.X - (a.X + t * dx), 2) + Math.Pow(p.Y - (a.Y + t * dy), 2));
    }
    private static double GeographyPointSegmentDistance(SqlSpatialCoordinate point, SqlSpatialCoordinate start,
        SqlSpatialCoordinate end)
    {
        var segmentLength = AngularDistance(start, end);
        if (segmentLength <= double.Epsilon) return SegmentLength(point, start, SqlSpatialKind.Geography);
        var pointDistance = AngularDistance(start, point);
        var crossTrack = Math.Asin(Math.Clamp(Math.Sin(pointDistance) *
            Math.Sin(InitialBearing(start, point) - InitialBearing(start, end)), -1, 1));
        var bearingDelta = InitialBearing(start, point) - InitialBearing(start, end);
        var alongTrack = Math.Atan2(Math.Sin(pointDistance) * Math.Cos(bearingDelta), Math.Cos(pointDistance));
        if (double.IsFinite(alongTrack) && alongTrack >= 0 && alongTrack <= segmentLength)
            return Math.Abs(crossTrack) * EarthRadius;
        return Math.Min(SegmentLength(point, start, SqlSpatialKind.Geography),
            SegmentLength(point, end, SqlSpatialKind.Geography));
    }
    private static double AngularDistance(SqlSpatialCoordinate a, SqlSpatialCoordinate b) =>
        SegmentLength(a, b, SqlSpatialKind.Geography) / EarthRadius;
    private static double InitialBearing(SqlSpatialCoordinate from, SqlSpatialCoordinate to)
    {
        var latitude1 = Degrees(from.Y); var latitude2 = Degrees(to.Y);
        var longitude = Degrees(to.X - from.X);
        return Math.Atan2(Math.Sin(longitude) * Math.Cos(latitude2),
            Math.Cos(latitude1) * Math.Sin(latitude2) - Math.Sin(latitude1) * Math.Cos(latitude2) * Math.Cos(longitude));
    }
    private static double SegmentLength(SqlSpatialCoordinate a, SqlSpatialCoordinate b, SqlSpatialKind kind)
    {
        if (kind == SqlSpatialKind.Geometry) return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        var lat1 = Degrees(a.Y); var lat2 = Degrees(b.Y); var dlat = lat2 - lat1; var dlon = Degrees(b.X - a.X);
        var h = Math.Pow(Math.Sin(dlat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dlon / 2), 2);
        return 2 * EarthRadius * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }
    private static double RingArea(IReadOnlyList<SqlSpatialCoordinate> ring, SqlSpatialKind kind)
    {
        if (kind == SqlSpatialKind.Geography)
        {
            double spherical = 0;
            for (var index = 1; index < ring.Count; index++)
            {
                var longitude = Degrees(ring[index].X - ring[index - 1].X);
                if (longitude > Math.PI) longitude -= 2 * Math.PI;
                else if (longitude < -Math.PI) longitude += 2 * Math.PI;
                spherical += longitude * (2 + Math.Sin(Degrees(ring[index - 1].Y)) + Math.Sin(Degrees(ring[index].Y)));
            }
            return Math.Abs(spherical) * EarthRadius * EarthRadius / 2;
        }
        double area = 0;
        for (var index = 1; index < ring.Count; index++) area += ring[index - 1].X * ring[index].Y - ring[index].X * ring[index - 1].Y;
        area = Math.Abs(area / 2);
        return area;
    }
    private static double Degrees(double value) => value * Math.PI / 180d;
}

internal static class SqlSpatialBinaryCodec
{
    private const byte Version = 2;
    public static byte[] Encode(SqlSpatialKind kind, int srid, SqlSpatialShape shape)
    {
        var output = new ArrayBufferWriter<byte>(); WriteByte(output, Version); WriteByte(output, (byte)kind); WriteInt(output, srid); WriteShape(output, shape);
        return output.WrittenSpan.ToArray();
    }
    public static (SqlSpatialKind Kind, int Srid, SqlSpatialShape Shape) Decode(ReadOnlySpan<byte> value)
    {
        var reader = new Reader(value); if (reader.Byte() != Version) throw new StorageFormatException("Unsupported spatial binary version.");
        var kind = (SqlSpatialKind)reader.Byte(); if (!Enum.IsDefined(kind)) throw new StorageFormatException("Invalid spatial kind.");
        var result = (kind, reader.Int(), reader.Shape()); if (!reader.End) throw new StorageFormatException("Spatial representation contains trailing bytes."); return result;
    }
    private static void WriteShape(IBufferWriter<byte> output, SqlSpatialShape shape)
    {
        WriteByte(output, (byte)shape.Type); WriteByte(output, shape.IsEmpty ? (byte)1 : (byte)0); WriteInt(output, shape.Points.Length);
        foreach (var point in shape.Points)
        {
            WriteDouble(output, point.X); WriteDouble(output, point.Y); var flags = (byte)((point.Z is null ? 0 : 1) | (point.M is null ? 0 : 2)); WriteByte(output, flags);
            if (point.Z is { } z) WriteDouble(output, z); if (point.M is { } m) WriteDouble(output, m);
        }
        WriteInt(output, shape.Children.Length); foreach (var child in shape.Children) WriteShape(output, child);
    }
    private static void WriteByte(IBufferWriter<byte> output, byte value) { var span = output.GetSpan(1); span[0] = value; output.Advance(1); }
    private static void WriteInt(IBufferWriter<byte> output, int value) { var span = output.GetSpan(4); BinaryPrimitives.WriteInt32LittleEndian(span, value); output.Advance(4); }
    private static void WriteDouble(IBufferWriter<byte> output, double value) { var span = output.GetSpan(8); BinaryPrimitives.WriteDoubleLittleEndian(span, value); output.Advance(8); }
    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _source; private int _offset; private int _nodes;
        public Reader(ReadOnlySpan<byte> source) { _source = source; _offset = 0; _nodes = 0; }
        public bool End => _offset == _source.Length;
        public byte Byte() { Require(1); return _source[_offset++]; }
        public int Int() { Require(4); var value = BinaryPrimitives.ReadInt32LittleEndian(_source[_offset..]); _offset += 4; return value; }
        public double Double() { Require(8); var value = BinaryPrimitives.ReadDoubleLittleEndian(_source[_offset..]); _offset += 8; if (!double.IsFinite(value)) throw new StorageFormatException("Spatial coordinate is not finite."); return value; }
        public SqlSpatialShape Shape()
        {
            if (++_nodes > 1_000_000) throw new StorageFormatException("Spatial representation has too many nodes.");
            var type = (SqlSpatialShapeType)Byte(); if (!Enum.IsDefined(type)) throw new StorageFormatException("Unknown spatial shape type.");
            var empty = Byte() switch { 0 => false, 1 => true, _ => throw new StorageFormatException("Invalid spatial empty marker.") };
            var pointCount = Count(); var points = new SqlSpatialCoordinate[pointCount];
            for (var index = 0; index < points.Length; index++)
            { var x = Double(); var y = Double(); var flags = Byte(); if (flags > 3) throw new StorageFormatException("Invalid spatial coordinate flags."); points[index] = new(x, y, (flags & 1) != 0 ? Double() : null, (flags & 2) != 0 ? Double() : null); }
            var childCount = Count(); var children = new SqlSpatialShape[childCount]; for (var index = 0; index < children.Length; index++) children[index] = Shape();
            return new SqlSpatialShape(type, points, children, empty);
        }
        private int Count() { var value = Int(); if (value is < 0 or > 1_000_000) throw new StorageFormatException("Invalid spatial collection count."); return value; }
        private void Require(int count) { if (_offset > _source.Length - count) throw new StorageFormatException("Spatial representation is truncated."); }
    }
}

internal static class SqlSpatialWkbCodec
{
    public static byte[] Encode(SqlSpatialShape shape)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        Write(writer, shape); return stream.ToArray();
    }
    public static SqlSpatialShape Decode(ReadOnlySpan<byte> value)
    {
        try
        {
            using var stream = new MemoryStream(value.ToArray(), writable: false); using var reader = new BinaryReader(stream);
            var shape = Read(reader); if (stream.Position != stream.Length) throw new StorageFormatException("WKB contains trailing bytes."); return shape;
        }
        catch (EndOfStreamException exception) { throw new StorageFormatException("WKB is truncated.", exception); }
    }
    private static void Write(BinaryWriter writer, SqlSpatialShape shape)
    {
        writer.Write((byte)1); writer.Write((uint)shape.Type switch
        {
            (uint)SqlSpatialShapeType.Point => 1u, (uint)SqlSpatialShapeType.LineString => 2u,
            (uint)SqlSpatialShapeType.Polygon => 3u, (uint)SqlSpatialShapeType.MultiPoint => 4u,
            (uint)SqlSpatialShapeType.MultiLineString => 5u, (uint)SqlSpatialShapeType.MultiPolygon => 6u,
            (uint)SqlSpatialShapeType.GeometryCollection => 7u,
            _ => throw new NotSupportedException("OGC WKB does not represent SQL curve or FULLGLOBE values.")
        });
        switch (shape.Type)
        {
            case SqlSpatialShapeType.Point: var point = shape.Points.Single(); writer.Write(point.X); writer.Write(point.Y); break;
            case SqlSpatialShapeType.LineString: WritePoints(writer, shape.Points); break;
            case SqlSpatialShapeType.Polygon:
                writer.Write((uint)shape.Children.Length); foreach (var ring in shape.Children) WritePoints(writer, ring.Points); break;
            default: writer.Write((uint)shape.Children.Length); foreach (var child in shape.Children) Write(writer, child); break;
        }
    }
    private static void WritePoints(BinaryWriter writer, IReadOnlyCollection<SqlSpatialCoordinate> points)
    { writer.Write((uint)points.Count); foreach (var point in points) { writer.Write(point.X); writer.Write(point.Y); } }
    private static SqlSpatialShape Read(BinaryReader reader)
    {
        var little = reader.ReadByte() switch { 0 => false, 1 => true, _ => throw new StorageFormatException("Invalid WKB byte order.") };
        var type = UInt32(reader, little); return type switch
        {
            1 => SqlSpatialShape.Point(Point(reader, little)),
            2 => new SqlSpatialShape(SqlSpatialShapeType.LineString, Points(reader, little)),
            3 => new SqlSpatialShape(SqlSpatialShapeType.Polygon, children: Enumerable.Range(0, Count(reader, little)).Select(_ => new SqlSpatialShape(SqlSpatialShapeType.LineString, Points(reader, little)))),
            4 => new SqlSpatialShape(SqlSpatialShapeType.MultiPoint, children: Children(reader, little, SqlSpatialShapeType.Point)),
            5 => new SqlSpatialShape(SqlSpatialShapeType.MultiLineString, children: Children(reader, little, SqlSpatialShapeType.LineString)),
            6 => new SqlSpatialShape(SqlSpatialShapeType.MultiPolygon, children: Children(reader, little, SqlSpatialShapeType.Polygon)),
            7 => new SqlSpatialShape(SqlSpatialShapeType.GeometryCollection, children: Children(reader, little, null)),
            _ => throw new StorageFormatException($"Unsupported WKB geometry type {type}.")
        };
    }
    private static SqlSpatialShape[] Children(BinaryReader reader, bool little, SqlSpatialShapeType? expected)
    {
        var children = new SqlSpatialShape[Count(reader, little)];
        for (var index = 0; index < children.Length; index++)
        { children[index] = Read(reader); if (expected is { } type && children[index].Type != type) throw new StorageFormatException("WKB collection contains the wrong shape type."); }
        return children;
    }
    private static SqlSpatialCoordinate[] Points(BinaryReader reader, bool little)
    { var points = new SqlSpatialCoordinate[Count(reader, little)]; for (var index = 0; index < points.Length; index++) points[index] = Point(reader, little); return points; }
    private static SqlSpatialCoordinate Point(BinaryReader reader, bool little) => new(Double(reader, little), Double(reader, little));
    private static int Count(BinaryReader reader, bool little) { var value = UInt32(reader, little); if (value > 1_000_000) throw new StorageFormatException("WKB collection is too large."); return checked((int)value); }
    private static uint UInt32(BinaryReader reader, bool little) { var bytes = reader.ReadBytes(4); if (bytes.Length != 4) throw new EndOfStreamException(); if (BitConverter.IsLittleEndian != little) Array.Reverse(bytes); return BitConverter.ToUInt32(bytes); }
    private static double Double(BinaryReader reader, bool little) { var bytes = reader.ReadBytes(8); if (bytes.Length != 8) throw new EndOfStreamException(); if (BitConverter.IsLittleEndian != little) Array.Reverse(bytes); var value = BitConverter.ToDouble(bytes); if (!double.IsFinite(value)) throw new StorageFormatException("WKB coordinate is not finite."); return value; }
}

internal static class SqlSpatialGml
{
    private static readonly XNamespace Gml = "http://www.opengis.net/gml";
    public static string Write(SqlSpatialShape shape, int srid) => Element(shape, srid).ToString(SaveOptions.DisableFormatting);
    private static XElement Element(SqlSpatialShape shape, int srid)
    {
        var attributes = new XAttribute("srsName", $"urn:ogc:def:crs:EPSG::{srid}");
        return shape.Type switch
        {
            SqlSpatialShapeType.Point => new XElement(Gml + "Point", attributes, new XElement(Gml + "pos", shape.Points.Single().ToWkt())),
            SqlSpatialShapeType.LineString => new XElement(Gml + "LineString", attributes,
                new XElement(Gml + "posList", string.Join(' ', shape.Points.Select(point => point.ToWkt())))),
            SqlSpatialShapeType.Polygon => new XElement(Gml + "Polygon", attributes,
                shape.Children.Select((ring, index) => new XElement(Gml + (index == 0 ? "exterior" : "interior"),
                    new XElement(Gml + "LinearRing", new XElement(Gml + "posList", string.Join(' ', ring.Points.Select(point => point.ToWkt()))))))),
            _ => new XElement(Gml + "MultiGeometry", attributes,
                shape.Children.Select(child => new XElement(Gml + "geometryMember", Element(child, srid))))
        };
    }
}
