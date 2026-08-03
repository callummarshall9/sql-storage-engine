using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class LockRulesTests
{
    private static readonly LockMode[] Modes = [LockMode.Shared, LockMode.Update, LockMode.Exclusive];

    [TestCase(LockMode.Shared, LockMode.Shared, true)]
    [TestCase(LockMode.Shared, LockMode.Update, true)]
    [TestCase(LockMode.Shared, LockMode.Exclusive, false)]
    [TestCase(LockMode.Update, LockMode.Shared, true)]
    [TestCase(LockMode.Update, LockMode.Update, false)]
    [TestCase(LockMode.Update, LockMode.Exclusive, false)]
    [TestCase(LockMode.Exclusive, LockMode.Shared, false)]
    [TestCase(LockMode.Exclusive, LockMode.Update, false)]
    [TestCase(LockMode.Exclusive, LockMode.Exclusive, false)]
    public void CompatibilityTable_CoversEveryModePair(LockMode first, LockMode second, bool expected)
    {
        LockRules.AreCompatible(first, second).Should().Be(expected);
    }

    [Test]
    public void ConversionTable_CoversEveryModePairAndRejectsInvalidConversions()
    {
        var valid = new HashSet<(LockMode, LockMode)>
        {
            (LockMode.Shared, LockMode.Shared), (LockMode.Shared, LockMode.Update),
            (LockMode.Shared, LockMode.Exclusive), (LockMode.Update, LockMode.Update),
            (LockMode.Update, LockMode.Exclusive), (LockMode.Exclusive, LockMode.Exclusive)
        };

        foreach (var pair in Modes.SelectMany(current => Modes.Select(requested => (current, requested))))
        {
            LockRules.CanConvert(pair.current, pair.requested).Should().Be(valid.Contains(pair));
            if (valid.Contains(pair)) LockRules.EnsureValidConversion(pair.current, pair.requested);
            else ((Action)(() => LockRules.EnsureValidConversion(pair.current, pair.requested)))
                .Should().Throw<InvalidOperationException>();
        }
    }

    [Test]
    public void ResourceIdentities_UseTypedStableIdentifiersAndValueEquality()
    {
        var tableId = new TableId(4);
        var rowId = new RowId(new PageId(8), new SlotId(2), new SlotGeneration(3));
        var keyBytes = new byte[] { 0x10, 0x20 };
        LockResource[] resources =
        [
            new TableLockResource(tableId),
            new RowLockResource(tableId, rowId),
            new IndexKeyLockResource(new IndexId(5), new IndexKey(keyBytes)),
            new IndexRangeLockResource(new IndexId(5), new IndexKey([0x10]), new IndexKey([0x30]))
        ];
        keyBytes[0] = 0xff;

        resources.Distinct().Should().HaveCount(4);
        resources[1].Should().Be(new RowLockResource(tableId, rowId));
        resources[2].Should().Be(new IndexKeyLockResource(new IndexId(5), new IndexKey([0x10, 0x20])));
    }

    [Test]
    public void InvalidModesAndDescendingRanges_AreRejected()
    {
        ((Func<bool>)(() => LockRules.AreCompatible((LockMode)0, LockMode.Shared)))
            .Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => new IndexRangeLockResource(new IndexId(1), new IndexKey([2]), new IndexKey([1]))))
            .Should().Throw<ArgumentException>();
    }
}
