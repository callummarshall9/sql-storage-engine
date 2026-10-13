using AwesomeAssertions;
using sql_storage_engine.Exposures.Indexes;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.UnitTests;

public sealed class IndexModelArchitectureTests
{
    [Test]
    public void KeySnapshotsAndValueEqualitySupportIndependentDictionaryKeys()
    {
        var source = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        var first = IndexKeyExposure.CreateIndexKey(source);
        var second = IndexKeyExposure.CreateIndexKey(source.ToArray());
        var expected = source.ToArray();
        source[0] = 99;
        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
        new Dictionary<IndexKey, int> { [first] = 42 }[second].Should().Be(42);
        IndexKeyExposure.ReadIndexKeyBytes(first).ToArray().Should().Equal(expected);
        IndexKeyExposure.FormatIndexKey(first).Should().Be(Convert.ToHexString(expected));
    }

    [Test]
    public void KeyOrderingMatchesUnsignedBytesIncludingZerosAndPrefixes()
    {
        byte[][] values = [ [0], [0, 0], [0, 1], [1], [1, 0], [0x7F], [0x80], [0xFF], [0xFF, 0] ];
        foreach (var left in values)
        foreach (var right in values)
        {
            var actual = IndexKeyExposure.CompareIndexKeys([
                IndexKeyExposure.CreateIndexKey(left), IndexKeyExposure.CreateIndexKey(right)]);
            Math.Sign(actual).Should().Be(Math.Sign(left.AsSpan().SequenceCompareTo(right)));
        }
        IndexKeyExposure.CompareIndexKeys([IndexKeyExposure.CreateIndexKey([1]), null]).Should().Be(1);
    }

    [Test]
    public void LeafPayloadSnapshotsKeepContentEqualityAndIndependentReads()
    {
        var key = IndexKeyExposure.CreateIndexKey([0x80, 0xFF]);
        var row = new RowId { PageId = new PageId { Value = 1 } };
        var payload = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        var first = LeafIndexEntryExposure.CreateLeafIndexEntry(key, row, payload);
        var second = LeafIndexEntryExposure.CreateLeafIndexEntry(key, row, payload.ToArray());
        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
        payload[0] = 99;
        var read = LeafIndexEntryExposure.ReadLeafIndexEntryPayload(first).ToArray();
        read[0] = 88;
        LeafIndexEntryExposure.ReadLeafIndexEntryPayload(first).Span[0].Should().Be(0);
    }

    [Test]
    public void InvalidDirectlyInitializedByteDataCannotBeDecodedOrCompared()
    {
        var invalid = new IndexKey { Content = "\u0100" };
        ((Action)(() => IndexKeyExposure.ReadIndexKeyBytes(invalid)))
            .Should().Throw<StorageModelValidationException>();
        ((Action)(() => IndexKeyExposure.CompareIndexKeys([invalid, null])))
            .Should().Throw<StorageModelValidationException>();
    }
}