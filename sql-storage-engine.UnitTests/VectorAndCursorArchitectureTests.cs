using System;
using System.Collections.Generic;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Catalog;

namespace sql_storage_engine.UnitTests;

public sealed class VectorAndCursorArchitectureTests
{
    [Test]
    public void VectorSnapshotsKeepExactFiniteBitsAndNormalizeSignedZero()
    {
        float[] source = [float.Epsilon, -1.5f, -0f];
        var vector = (VectorSqlValue)SqlValueExposure.CreateVectorSqlValue(source);
        source[0] = 10;
        SqlValueExposure.ReadVectorSqlValueValues(vector).Should().Equal(float.Epsilon, -1.5f, 0f);
        vector.Should().Be(SqlValueExposure.CreateVectorSqlValue([float.Epsilon, -1.5f, 0f]));
        var projection = (IList<float>)SqlValueExposure.ReadVectorSqlValueValues(vector);
        Action mutate = () => projection[0] = 1;
        mutate.Should().Throw<NotSupportedException>();
        var half = (VectorSqlValue)SqlValueExposure.CreateHalfVectorSqlValue([(Half)1.5f, Half.Epsilon]);
        SqlValueExposure.ReadVectorSqlValueValues(half).Should().Equal(1.5f, (float)Half.Epsilon);
    }

    [Test]
    public void VectorDistancePreservesMetricAndDimensionRules()
    {
        var left = (VectorSqlValue)SqlValueExposure.CreateVectorSqlValue([3, 4]);
        var right = (VectorSqlValue)SqlValueExposure.CreateHalfVectorSqlValue([(Half)0, (Half)0]);
        SqlValueExposure.MeasureVectorSqlValueDistance([left, right],SqlVectorDistanceMetric.Euclidean).Should().Be(5);
        SqlValueExposure.MeasureVectorSqlValueDistance([left, left],SqlVectorDistanceMetric.DotProduct).Should().Be(-25);
        double.IsNaN(SqlValueExposure.MeasureVectorSqlValueDistance([left, right])).Should().BeTrue();
        Action mismatch = () => SqlValueExposure.MeasureVectorSqlValueDistance([left, (VectorSqlValue)SqlValueExposure.CreateVectorSqlValue([1])]);
        mismatch.Should().Throw<ArgumentException>();
    }

    [Test]
    public void InvalidVectorRepresentationsAreRejectedByReadsAndRowAdmission()
    {
        var template = (VectorSqlValue)SqlValueExposure.CreateVectorSqlValue([1]);
        VectorSqlValue[] invalid = [template with { Content = "x" }, template with { BaseType = (SqlVectorBaseType)255 }, template with { Content = "\0\u7f80" }, template with { Content = null! }];
        foreach (var vector in invalid)
        {
            Action read = () => SqlValueExposure.ReadVectorSqlValueValues(vector);
            read.Should().Throw<ArgumentException>();
            Action admit = () => RowExposure.CreateRow([vector]);
            admit.Should().Throw<ArgumentException>();
        }
    }

    [Test]
    public void CursorCreationSnapshotsRowsAndNavigationPreservesBoundaries()
    {
        var first = RowExposure.CreateRow([SqlValueExposure.CreateIntegerSqlValue(1)]);
        var second = RowExposure.CreateRow([SqlValueExposure.CreateIntegerSqlValue(2)]);
        var rows = new List<Row> { first, second };
        var cursor = (CursorSqlValue)SqlValueExposure.CreateCursorSqlValue(rows);
        rows.Clear();
        cursor = CursorSqlValueExposure.FetchCursorSqlValue(cursor,"Next",0,out var fetched);
        fetched.Should().BeSameAs(first);
        cursor = CursorSqlValueExposure.FetchCursorSqlValue(cursor,"Absolute",-1,out fetched);
        fetched.Should().BeSameAs(second);
        cursor = CursorSqlValueExposure.FetchCursorSqlValue(cursor,"Next",0,out fetched);
        fetched.Should().BeNull();
        cursor = CursorSqlValueExposure.FetchCursorSqlValue(cursor,"Prior",0,out fetched);
        fetched.Should().BeSameAs(second);
        cursor = CursorSqlValueExposure.CloseCursorSqlValue(cursor);
        Action closed = () => CursorSqlValueExposure.FetchCursorSqlValue(cursor,"First",0,out _);
        closed.Should().Throw<Exception>().Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }
}