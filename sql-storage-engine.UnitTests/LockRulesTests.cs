using AwesomeAssertions;
using sql_storage_engine.Exposures.Locking;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Exposures.LockResources;

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
        LockRulesExposure.AreCompatible(first,second).Should().Be(expected);
    }

    [Test]
    public void ConversionTable_CoversEveryModePairAndRejectsInvalidConversions()
    {
        var valid = new HashSet<(LockMode, LockMode)>
        {
            (LockMode.Shared, LockMode.Shared),
            (LockMode.Shared, LockMode.Update),
            (LockMode.Shared, LockMode.Exclusive),
            (LockMode.Update, LockMode.Update),
            (LockMode.Update, LockMode.Exclusive),
            (LockMode.Exclusive, LockMode.Exclusive)
        };
        foreach (var pair in Modes.SelectMany(current => Modes.Select(requested => (current, requested))))
        {
            LockRulesExposure.CanConvert(pair.current, pair.requested).Should().Be(valid.Contains(pair));
            if (valid.Contains(pair))
                LockRulesExposure.EnsureValidConversion(pair.current, pair.requested);
            else
                ((Action)(() => LockRulesExposure.EnsureValidConversion(pair.current, pair.requested))).Should().Throw<InvalidOperationException>();
        }
    }

    [Test]
    public void ResourceIdentities_UseTypedStableIdentifiersAndValueEquality()
    {
        var tableId = new sql_storage_engine.Models.Identifiers.TableId
        {
            Value = 4
        };
        var rowId = new sql_storage_engine.Models.Identifiers.RowId
        {
            PageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 8
            },
            SlotId = new sql_storage_engine.Models.Identifiers.SlotId
            {
                Value = 2
            },
            Generation = new sql_storage_engine.Models.Identifiers.SlotGeneration
            {
                Value = 3
            }
        };
        var keyBytes = new byte[]
        {
            0x10,
            0x20
        };
        LockResource[] resources = [sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateTableLockResource(new sql_storage_engine.Models.LockResources.TableLockResource { TableId = tableId }), sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateRowLockResource(new sql_storage_engine.Models.LockResources.RowLockResource { TableId = tableId, RowId = rowId }), sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateIndexKeyLockResource(new sql_storage_engine.Models.LockResources.IndexKeyLockResource { IndexId = new sql_storage_engine.Models.Identifiers.IndexId { Value = 5 }, Key = sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: keyBytes) }), sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateIndexRangeLockResource(new sql_storage_engine.Models.LockResources.IndexRangeLockResource { IncludeLowerBound = true, IncludeUpperBound = true, IndexId = new sql_storage_engine.Models.Identifiers.IndexId { Value = 5 }, LowerBound = sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: [0x10]), UpperBound = sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: [0x30]) })];
        keyBytes[0] = 0xff;
        resources.Distinct().Should().HaveCount(4);
        resources[1].Should().Be(sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateRowLockResource(new sql_storage_engine.Models.LockResources.RowLockResource { TableId = tableId, RowId = rowId }));
        resources[2].Should().Be(sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateIndexKeyLockResource(new sql_storage_engine.Models.LockResources.IndexKeyLockResource { IndexId = new sql_storage_engine.Models.Identifiers.IndexId { Value = 5 }, Key = sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: [0x10, 0x20]) }));
    }

    [Test]
    public void InvalidModesAndDescendingRanges_AreRejected()
    {
        ((Func<bool>)(() => LockRulesExposure.AreCompatible((LockMode)0,LockMode.Shared))).Should().Throw<StorageModelValidationException>().Which.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        ((Action)(() => sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateIndexRangeLockResource(new sql_storage_engine.Models.LockResources.IndexRangeLockResource { IncludeLowerBound = true, IncludeUpperBound = true, IndexId = new sql_storage_engine.Models.Identifiers.IndexId { Value = 1 }, LowerBound = sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: [2]), UpperBound = sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: [1]) }))).Should().Throw<ArgumentException>();
    }
}