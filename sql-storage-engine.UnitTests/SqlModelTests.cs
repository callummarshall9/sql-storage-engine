using AwesomeAssertions;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class SqlModelTests
{
    [Test]
    public void EveryInitialType_HasTypedRepresentationAndNullIsDistinctFromDefaults()
    {
        SqlValue.Boolean(false).Should().BeOfType<BooleanSqlValue>();
        SqlValue.Integer(0).Should().BeOfType<IntegerSqlValue>();
        SqlValue.Text(string.Empty).Should().BeOfType<TextSqlValue>();
        SqlValue.Binary(ReadOnlySpan<byte>.Empty).Should().BeOfType<BinarySqlValue>();
        SqlValue.Null.Should().BeOfType<NullSqlValue>();
        SqlValue.Null.Should().NotBe(SqlValue.Boolean(false));
        SqlValue.Null.Should().NotBe(SqlValue.Integer(0));
        SqlValue.Null.IsNull.Should().BeTrue();
    }

    [Test]
    public void Schema_RejectsNullForNonNullableAndMismatchedTypes()
    {
        var column = new ColumnDefinition(new ColumnId(1), "enabled", SqlType.Boolean, false);
        ((Action)(() => column.Validate(SqlValue.Null))).Should().Throw<ArgumentException>();
        ((Action)(() => column.Validate(SqlValue.Integer(1)))).Should().Throw<ArgumentException>();
        ((Action)(() => column.Validate(SqlValue.Boolean(true)))).Should().NotThrow();
    }

    [Test]
    public void RuntimeConversion_RejectsUnsupportedRepresentationsBeforeEncoding()
    {
        ((Func<SqlValue>)(() => SqlValue.From(12.5))).Should().Throw<ArgumentException>();
        ((Func<SqlValue>)(() => SqlValue.From(42))).Should().Throw<ArgumentException>();
        SqlValue.From(42L).Should().Be(SqlValue.Integer(42));
    }

    [Test]
    public void Comparison_UsesDocumentedNullAndTypeRules()
    {
        SqlValue.Compare(SqlValue.Null, SqlValue.Null).Should().Be(SqlComparison.Unknown);
        SqlValue.Compare(SqlValue.Integer(1), SqlValue.Integer(2)).Should().Be(SqlComparison.Less);
        SqlValue.Compare(SqlValue.Text("a"), SqlValue.Text("a")).Should().Be(SqlComparison.Equal);
        SqlValue.Compare(SqlValue.Binary(new byte[] { 1, 2 }), SqlValue.Binary(new byte[] { 1, 3 }))
            .Should().Be(SqlComparison.Less);
        ((Func<SqlComparison>)(() => SqlValue.Compare(SqlValue.Integer(1), SqlValue.Text("1"))))
            .Should().Throw<ArgumentException>();
    }
}
