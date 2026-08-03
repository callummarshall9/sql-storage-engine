using AwesomeAssertions;
using sql_storage_engine.Identifiers;

namespace sql_storage_engine.UnitTests;

public sealed class IdentifierTests
{
    [Test]
    public void Identifiers_WithEqualValues_SupportValueEqualityAndDictionaryKeys()
    {
        var values = new Dictionary<PageId, string> { [new PageId(42)] = "answer" };

        new PageId(42).Should().Be(new PageId(42));
        new PageId(42).Should().NotBe(new PageId(43));
        values[new PageId(42)].Should().Be("answer");
    }

    [Test]
    public void DatabaseId_New_ReturnsDistinctNonEmptyValues()
    {
        var first = DatabaseId.New();
        var second = DatabaseId.New();

        first.Value.Should().NotBe(Guid.Empty);
        second.Should().NotBe(first);
    }

    [Test]
    public void Identifiers_DefaultAndMaximumValues_ArePreserved()
    {
        default(PageId).Value.Should().Be(0);
        default(LogSequenceNumber).Value.Should().Be(0);
        new PageId(ulong.MaxValue).Value.Should().Be(ulong.MaxValue);
        new TableId(ulong.MaxValue).Value.Should().Be(ulong.MaxValue);
        new IndexId(ulong.MaxValue).Value.Should().Be(ulong.MaxValue);
        new TransactionId(ulong.MaxValue).Value.Should().Be(ulong.MaxValue);
        new LogSequenceNumber(ulong.MaxValue).Value.Should().Be(ulong.MaxValue);
        new SlotId(ushort.MaxValue).Value.Should().Be(ushort.MaxValue);
        new SlotGeneration(uint.MaxValue).Value.Should().Be(uint.MaxValue);
    }

    [Test]
    public void NullableIdentifier_RepresentsAbsenceWithoutSentinel()
    {
        PageId? absent = null;
        PageId? pageZero = new PageId(0);

        absent.Should().BeNull();
        pageZero.Should().NotBeNull();
        pageZero!.Value.Value.Should().Be(0);
    }

    [Test]
    public void Identifiers_ToString_UsesStableKindAndValueFormatting()
    {
        var databaseGuid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        new DatabaseId(databaseGuid).ToString().Should().Be("database:00112233-4455-6677-8899-aabbccddeeff");
        new PageId(1).ToString().Should().Be("page:1");
        new TableId(2).ToString().Should().Be("table:2");
        new IndexId(3).ToString().Should().Be("index:3");
        new TransactionId(4).ToString().Should().Be("transaction:4");
        new LogSequenceNumber(5).ToString().Should().Be("lsn:5");
        new SlotId(6).ToString().Should().Be("slot:6");
        new SlotGeneration(7).ToString().Should().Be("generation:7");
        new RowId(new PageId(1), new SlotId(6), new SlotGeneration(7)).ToString()
            .Should().Be("row:1/6/7");
    }
}
