using AwesomeAssertions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class SqlHierarchyArchitectureTests
{
    [Test]
    public void BinaryBytesAndCanonicalPathEqualityRemainStable()
    {
        var value = SqlHierarchyIdExposure.ParseSqlHierarchyId("/1/-2.0/");
        var bytes = SqlHierarchyIdExposure.WriteSqlHierarchyId(value);
        Convert.ToHexString(bytes).Should().Be("02020102010102000102010100");
        SqlHierarchyIdExposure.ReadSqlHierarchyId(bytes).Should().Be(value);
        SqlHierarchyIdExposure.FormatSqlHierarchyId(value).Should().Be("/1/-2.0/");
        SqlHierarchyIdExposure.ReadSqlHierarchyIdLevel(value).Should().Be(2);
        SqlHierarchyIdExposure.WriteSqlHierarchyId(SqlHierarchyIdExposure.ReadRootSqlHierarchyId()).Should().Equal(2, 0);
        ((Action)(() => SqlHierarchyIdExposure.WriteSqlHierarchyId(new SqlHierarchyId { Path = "/01/" }))).Should().Throw<FormatException>();
    }

    [Test]
    public void NavigationAndSortKeysRetainDenseSiblingOrdering()
    {
        var parent = SqlHierarchyIdExposure.ParseSqlHierarchyId("/1/");
        var left = SqlHierarchyIdExposure.ParseSqlHierarchyId("/1/2/");
        var right = SqlHierarchyIdExposure.ParseSqlHierarchyId("/1/2.1/");
        var middle = SqlHierarchyIdExposure.CreateSqlHierarchyIdDescendant([parent, left, right]);
        SqlHierarchyIdExposure.FormatSqlHierarchyId(middle).Should().Be("/1/2.0/");
        SqlHierarchyIdExposure.CompareSqlHierarchyIds([left, middle]).Should().BeNegative();
        SqlHierarchyIdExposure.CompareSqlHierarchyIds([middle, right]).Should().BeNegative();
        SqlHierarchyIdExposure.ReadSqlHierarchyIdSortKey(left).AsSpan().SequenceCompareTo(SqlHierarchyIdExposure.ReadSqlHierarchyIdSortKey(middle)).Should().BeNegative();
        SqlHierarchyIdExposure.ReadSqlHierarchyIdAncestor(middle,1).Should().Be(parent);
        SqlHierarchyIdExposure.ReadSqlHierarchyIdAncestor(middle,3).Should().BeNull();
        var newRoot = SqlHierarchyIdExposure.ParseSqlHierarchyId("/9/");
        SqlHierarchyIdExposure.FormatSqlHierarchyId(SqlHierarchyIdExposure.ReparentSqlHierarchyId([middle, parent, newRoot])).Should().Be("/9/2.0/");
        Parallel.For(0, 64, _ => SqlHierarchyIdExposure.ReadSqlHierarchyId(SqlHierarchyIdExposure.WriteSqlHierarchyId(middle)).Should().Be(middle));
    }

    [Test]
    public void MalformedBinaryCountsAreRejectedBeforeAllocation()
    {
        foreach(var bytes in new[] { new byte[] { 2, 255, 255, 255, 255, 15 }, new byte[] { 2, 255, 255, 255, 255, 31 }, new byte[] { 2, 1, 255, 255, 255, 255, 15 }, new byte[] { 2, 1, 0 } })
            ((Action)(() => SqlHierarchyIdExposure.ReadSqlHierarchyId(bytes))).Should().Throw<StorageFormatException>();
    }
}