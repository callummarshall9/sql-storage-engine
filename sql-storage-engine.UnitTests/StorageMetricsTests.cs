using System.Diagnostics.Metrics;
using AwesomeAssertions;
using sql_storage_engine.Diagnostics;

namespace sql_storage_engine.UnitTests;

public sealed class StorageMetricsTests
{
    [Test]
    public void CountersHistogramsAndGauges_ExposeRequiredSignalsWithBoundedTags()
    {
        var measurements = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        { if (instrument.Meter.Name == StorageMetrics.MeterName) meterListener.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();
        using var metrics = new StorageMetrics("tests");

        metrics.RecordBufferAccess(true); metrics.RecordBufferAccess(false);
        metrics.RecordPageRead(true); metrics.RecordPageWrite(false);
        metrics.RecordWalAppend(42, true); metrics.RecordTransaction("commit"); metrics.RecordTransaction("rollback");
        metrics.RecordDeadlock(); metrics.RecordBTreeSplit(); metrics.RecordBTreeMerge();
        metrics.SetBufferFrames(2, 3); metrics.SetHeapBytes(100, 20); metrics.SetBTreeHeight(4);
        metrics.SetOverflowBytes(50); metrics.SetRecoveryDistance(60);
        listener.RecordObservableInstruments();

        measurements.Select(item => item.Name).Should().Contain([
            "storage.buffer.hits", "storage.buffer.misses", "storage.page.reads", "storage.page.writes",
            "storage.wal.bytes", "storage.transactions", "storage.transactions.deadlocks", "storage.btree.splits",
            "storage.btree.merges", "storage.buffer.pinned", "storage.buffer.dirty", "storage.heap.live.bytes",
            "storage.heap.dead.bytes", "storage.btree.height", "storage.overflow.bytes", "storage.recovery.distance"]);
        measurements.SelectMany(item => item.Tags).Select(tag => tag.Key).Distinct().Should().OnlyContain(key => key == "outcome");
    }

    [Test]
    public void SuccessAndFailureMetrics_DoNotChangeCallerBehavior()
    {
        var values = new List<double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => meterListener.EnableMeasurementEvents(instrument);
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => values.Add(value));
        listener.Start();
        using var metrics = new StorageMetrics("failure-tests");

        metrics.RecordPageFlush(TimeSpan.FromMilliseconds(3), true);
        metrics.RecordPageFlush(TimeSpan.FromMilliseconds(5), false);
        metrics.RecordWalFlush(TimeSpan.FromMilliseconds(7), false);
        metrics.RecordCheckpoint(TimeSpan.FromMilliseconds(11), true);

        values.Should().BeEquivalentTo([3d, 5d, 7d, 11d]);
    }
}
