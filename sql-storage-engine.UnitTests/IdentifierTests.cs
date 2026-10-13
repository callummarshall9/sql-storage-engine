using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Exposures.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class IdentifierTests
{
    [Test]
    public void Identifiers_WithEqualValues_SupportValueEqualityAndDictionaryKeys()
    {
        var values = new Dictionary<PageId, string>
        {
            [new sql_storage_engine.Models.Identifiers.PageId { Value = 42 }] = "answer"
        };
        new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = 42
        }.Should().Be(new sql_storage_engine.Models.Identifiers.PageId { Value = 42 });
        new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = 42
        }.Should().NotBe(new sql_storage_engine.Models.Identifiers.PageId { Value = 43 });
        values[new sql_storage_engine.Models.Identifiers.PageId { Value = 42 }].Should().Be("answer");
    }

    [Test]
    public void DatabaseId_New_ReturnsDistinctNonEmptyValues()
    {
        var first = DatabaseIdExposure.CreateDatabaseId();
        var second = DatabaseIdExposure.CreateDatabaseId();
        first.Value.Should().NotBe(Guid.Empty);
        second.Should().NotBe(first);
    }

    [Test]
    public void Identifiers_DefaultAndMaximumValues_ArePreserved()
    {
        default(PageId).Value.Should().Be(0);
        default(LogSequenceNumber).Value.Should().Be(0);
        new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = ulong.MaxValue
        }.Value.Should().Be(ulong.MaxValue);
        new sql_storage_engine.Models.Identifiers.TableId
        {
            Value = ulong.MaxValue
        }.Value.Should().Be(ulong.MaxValue);
        new sql_storage_engine.Models.Identifiers.IndexId
        {
            Value = ulong.MaxValue
        }.Value.Should().Be(ulong.MaxValue);
        new sql_storage_engine.Models.Identifiers.TransactionId
        {
            Value = ulong.MaxValue
        }.Value.Should().Be(ulong.MaxValue);
        new sql_storage_engine.Models.Identifiers.LogSequenceNumber
        {
            Value = ulong.MaxValue
        }.Value.Should().Be(ulong.MaxValue);
        new sql_storage_engine.Models.Identifiers.SlotId
        {
            Value = ushort.MaxValue
        }.Value.Should().Be(ushort.MaxValue);
        new sql_storage_engine.Models.Identifiers.SlotGeneration
        {
            Value = uint.MaxValue
        }.Value.Should().Be(uint.MaxValue);
    }

    [Test]
    public void NullableIdentifier_RepresentsAbsenceWithoutSentinel()
    {
        PageId? absent = null;
        PageId? pageZero = new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = 0
        };
        absent.Should().BeNull();
        pageZero.Should().NotBeNull();
        pageZero!.Value.Value.Should().Be(0);
    }

    [Test]
    public void Identifiers_DisplayExposure_UsesStableKindAndValueFormatting()
    {
        var databaseGuid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        IdentifierDisplayExposure.FormatDatabaseId(new sql_storage_engine.Models.Identifiers.DatabaseId { Value = databaseGuid }).Should().Be("database:00112233-4455-6677-8899-aabbccddeeff");
        IdentifierDisplayExposure.FormatPageId(new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }).Should().Be("page:1");
        IdentifierDisplayExposure.FormatTableId(new sql_storage_engine.Models.Identifiers.TableId { Value = 2 }).Should().Be("table:2");
        IdentifierDisplayExposure.FormatIndexId(new sql_storage_engine.Models.Identifiers.IndexId { Value = 3 }).Should().Be("index:3");
        IdentifierDisplayExposure.FormatTransactionId(new sql_storage_engine.Models.Identifiers.TransactionId { Value = 4 }).Should().Be("transaction:4");
        IdentifierDisplayExposure.FormatLogSequenceNumber(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 5 }).Should().Be("lsn:5");
        IdentifierDisplayExposure.FormatSlotId(new sql_storage_engine.Models.Identifiers.SlotId { Value = 6 }).Should().Be("slot:6");
        IdentifierDisplayExposure.FormatSlotGeneration(new sql_storage_engine.Models.Identifiers.SlotGeneration { Value = 7 }).Should().Be("generation:7");
        IdentifierDisplayExposure.FormatRowId(new sql_storage_engine.Models.Identifiers.RowId { PageId = new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, SlotId = new sql_storage_engine.Models.Identifiers.SlotId { Value = 6 }, Generation = new sql_storage_engine.Models.Identifiers.SlotGeneration { Value = 7 } }).Should().Be("row:1/6/7");
    }

    [Test]
    public void CompoundIdentifierJsonRoundTripPreservesEveryComponent()
    {
        var rowId = new RowId
        {
            PageId = new PageId
            {
                Value = ulong.MaxValue
            },
            SlotId = new SlotId
            {
                Value = ushort.MaxValue
            },
            Generation = new SlotGeneration
            {
                Value = uint.MaxValue
            }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(rowId);
        System.Text.Json.JsonSerializer.Deserialize<RowId>(json).Should().Be(rowId);
        var databaseId = DatabaseIdExposure.CreateDatabaseId();
        var databaseJson = System.Text.Json.JsonSerializer.Serialize(databaseId);
        System.Text.Json.JsonSerializer.Deserialize<DatabaseId>(databaseJson).Should().Be(databaseId);
    }
}