using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class JsonPayloadArchitectureTests
{
    [TestCase("null", "0100")]
    [TestCase("false", "0101")]
    [TestCase("true", "0102")]
    [TestCase("12.50", "01030500000031322E3530")]
    [TestCase("\"é\"", "010402000000C3A9")]
    [TestCase("[null,true]", "0105020000000002")]
    [TestCase("{\"x\":1}", "0106010000000100000078030100000031")]
    public void JsonPayloadMatchesGoldenBytes(string json, string hex)
    {
        var encoded = JsonPayloadExposure.EncodeJsonPayload(json);
        encoded.Should().Equal(Convert.FromHexString(hex));
        using var document = JsonDocument.Parse(JsonPayloadExposure.DecodeJsonPayload(encoded));
        document.RootElement.GetRawText().Should().Be(json.Replace("é","\\u00E9"));
    }

    [Test]
    public void DuplicatePropertiesOrderAndNumericSpellingSurviveRoundTrip()
    {
        const string json = "{\"x\":1e+3,\"x\":-0.0,\"a\":[\"😀\",null]}";
        var roundTrip = JsonPayloadExposure.DecodeJsonPayload(JsonPayloadExposure.EncodeJsonPayload(json));
        using var document = JsonDocument.Parse(roundTrip);
        var properties = document.RootElement.EnumerateObject().ToArray();
        properties.Select(property => property.Name).Should().Equal("x", "x", "a");
        properties[0].Value.GetRawText().Should().Be("1e+3");
        properties[1].Value.GetRawText().Should().Be("-0.0");
        Parallel.For(0, 64, _ => JsonPayloadExposure.DecodeJsonPayload(JsonPayloadExposure.EncodeJsonPayload(json)).Should().Be(roundTrip));
    }

    [Test]
    public void MalformedLengthsTokensUtf8AndExcessiveDepthAreRejected()
    {
        foreach (var hex in new[] { "00", "01FF", "010501000000", "0105FFFFFFFF", "010001", "01040900000061" })
            ((Func<string>)(() => JsonPayloadExposure.DecodeJsonPayload(Convert.FromHexString(hex)))).Should().Throw<StorageFormatException>();
        ((Func<string>)(() => JsonPayloadExposure.DecodeJsonPayload(Convert.FromHexString("010401000000FF")))).Should().Throw<DecoderFallbackException>();
        var deep = new List<byte> { 1 };
        for (var depth = 0; depth < 130; depth++) deep.AddRange(new byte[] { 5, 1, 0, 0, 0 });
        deep.Add(0);
        ((Func<string>)(() => JsonPayloadExposure.DecodeJsonPayload(deep.ToArray()))).Should().Throw<StorageFormatException>();
        ((Func<byte[]>)(() => JsonPayloadExposure.EncodeJsonPayload("{"))).Should().Throw<JsonException>();
    }
}