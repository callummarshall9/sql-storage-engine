using AwesomeAssertions;
using sql_storage_engine;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;

using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.UnitTests;

public sealed class GraphIdentityArchitectureTests
{
    [Test]
    public void GraphOwnershipEncodingKeepsExactBytesAndKindIsolation()
    {
        var databaseId = new DatabaseId { Value = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff") };
        var tableId = new TableId { Value = 0x0102030405060708 };
        var node = sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.CreateGraphNodeId(databaseId:databaseId,tableId:tableId,schemaVersion:0x1112131415161718,value:Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100"));
        var encoded = sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadBinarySqlValue(binarySqlValue: ((BinarySqlValue)sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.ToSqlValue(graphNodeId:node))).ToArray();
        Convert.ToHexString(encoded).Should().Be("010133221100554477668899AABBCCDDEEFF08070605040302011817161514131211CCDDEEFFAABB88997766554433221100");
sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.FromSqlValue(sqlValue:sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(encoded)).Should().Be(node);
        ((Action)(() => sql_storage_engine.Exposures.Graph.GraphEdgeIdExposure.FromSqlValue(sqlValue:sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(encoded)))).Should().Throw<ArgumentException>();
        Parallel.For(0, 64, _ => sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.FromSqlValue(sqlValue:sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.ToSqlValue(graphNodeId:node)).Should().Be(node));
    }

    [Test]
    public void MalformedOwnershipRetainsTheCallerParameterAndCause()
    {
        var encoded = new byte[50];
        encoded[0] = 1;
        encoded[1] = 1;
        var failure = ((Action)(() => sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.FromSqlValue(sqlValue:sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(encoded)))).Should().Throw<ArgumentException>().Which;
        failure.ParamName.Should().Be("value");
        failure.InnerException.Should().BeOfType<ArgumentException>();
        ((Action)(() => sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.FromSqlValue(sqlValue:sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("invalid")))).Should().Throw<ArgumentException>();
        ((Action)(() => sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.FromSqlValue(sqlValue:null!))).Should().Throw<ArgumentNullException>();
    }
}