using AwesomeAssertions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.UnitTests;
public sealed class SqlTypeArchitectureTests
{
    [Test]
    public void RawInvalidDeclarationsFailAtSchemaAdmission()
    {
        var invalid = new SqlType { Name = SqlTypeName.Decimal, Precision = 0, Scale = 0 };
        ((Action)(() => ColumnDefinitionExposure.CreateColumnDefinition(new ColumnId { Value = 1 },"invalid",invalid,false))).Should().Throw<ArgumentException>();
        var valid = SqlTypeExposure.CreateDecimalSqlType(18,4);
        SqlTypeExposure.FormatSqlType(valid).Should().Be("decimal(18,4)");
        SqlTypeExposure.ReadSqlTypeStorageFamily(valid).Should().Be(SqlStorageFamily.Decimal);
        SqlTypeExposure.ConvertSqlTypeValue([SqlTypeExposure.CreateVarCharSqlType(3), SqlTypeExposure.ReadSqlTypeInt()],sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(123)).Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("123"));
    }

    [Test]
    public void XmlCollectionsKeepValueEqualityAndIndependentDefinitions()
    {
        const string definition = "<xs:schema xmlns:xs='http://www.w3.org/2001/XMLSchema'><xs:element name='number' type='xs:int'/></xs:schema>";
        var source = new[] { definition };
        var first = SqlXmlSchemaCollectionExposure.CreateSqlXmlSchemaCollection("dbo","Numbers",source);
        var second = SqlXmlSchemaCollectionExposure.CreateSqlXmlSchemaCollection("dbo","Numbers",definition);
        source[0] = "changed";
        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
        SqlXmlSchemaCollectionExposure.ReadSqlXmlSchemaCollectionDefinition(first).Should().Be(definition);
        var typed = SqlTypeExposure.CreateTypedXmlSqlType(first,SqlXmlContentKind.Document);
        SqlTypeExposure.CheckSqlTypeValue(typed,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<number>12</number>"),"value");
        ((Action)(() => SqlTypeExposure.CheckSqlTypeValue(typed,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<number>invalid</number>"),"value"))).Should().Throw<ArgumentException>();
        Parallel.For(0, 32, _ => SqlTypeExposure.CheckSqlTypeValue(typed,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("<number>12</number>"),"value"));
    }

    [Test]
    public void JsonStructuralObservationsPreserveUtf8AndCollectionBounds()
    {
        var type = SqlTypeExposure.ReadSqlTypeJson();
        SqlTypeExposure.CheckSqlTypeValue(type,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("{\"" + new string('x', 7998) + "\":0}"),"value");
        ((Action)(() => SqlTypeExposure.CheckSqlTypeValue(type,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("{\"" + new string('x', 7999) + "\":0}"),"value"))).Should().Throw<ArgumentOutOfRangeException>();
        var oversizedArray = "[" + string.Join(',', Enumerable.Repeat("0", 65536)) + "]";
        ((Action)(() => SqlTypeExposure.CheckSqlTypeValue(type,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(oversizedArray),"value"))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => SqlTypeExposure.CheckSqlTypeValue(type,sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("true"),"value"))).Should().Throw<ArgumentException>();
    }
}