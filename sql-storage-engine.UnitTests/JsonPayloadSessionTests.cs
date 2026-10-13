// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.UnitTests;

public sealed class JsonPayloadSessionTests
{
    [Test]
    public async Task SharedRowCodec_ReusesJsonSessionsAcrossConcurrentRoundTrips()
    {
        var schema = TableDefinitionExposure.CreateTableDefinition(columnDefinitions:
        [
            ColumnDefinitionExposure.CreateColumnDefinition(columnId: new ColumnId { Value = 1 }, name: "document", sqlType: SqlTypeExposure.ReadSqlTypeJson(), isNullable: false)
        ]);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (var iteration = 0; iteration < 32; iteration++)
            {
                var value = worker * 32 + iteration;
                var json = "{\"sequence\":" + value + ",\"values\":[null,true,\"q\"]}";
                var row = RowExposure.CreateRow(values: [SqlValueExposure.CreateTextSqlValue(value: json)]);
                var encoded = RowCodecExposure.Encode(row: row, tableDefinition: schema);
                var decoded = RowCodecExposure.Decode(source: encoded, tableDefinition: schema);
                ((TextSqlValue)decoded.Values[0]).Value.Should().Be(json);
            }
        })));
    }
}
