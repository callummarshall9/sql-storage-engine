using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class BufferFrameTests
{
    [Test]
    public void Pin_IncrementsAndDisposeDecrementsExactlyOnce()
    {
        sql_storage_engine.Exposures.Buffers.BufferFrameExposure frame = new sql_storage_engine.Exposures.Buffers.BufferFrameExposure(new sql_storage_engine.Models.Identifiers.PageId { Value = 4 }, new byte[128]);
        var pin = frame.Pin();
        frame.ReadPinCount().Should().Be(1);
        frame.ReadIsEvictable().Should().BeFalse();
        pin.Dispose();
        pin.Dispose();
        frame.ReadPinCount().Should().Be(0);
        frame.ReadIsEvictable().Should().BeTrue();
    }

    [Test]
    public void MarkDirty_RecordsSuppliedPageLsn()
    {
        sql_storage_engine.Exposures.Buffers.BufferFrameExposure frame = new sql_storage_engine.Exposures.Buffers.BufferFrameExposure(new sql_storage_engine.Models.Identifiers.PageId { Value = 7 }, new byte[128]);
        using var pin = frame.Pin();
        pin.MarkDirty(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 99 });
        frame.ReadIsDirty().Should().BeTrue();
        frame.ReadPageLogSequenceNumber().Should().Be(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 99 });
    }

    [Test]
    public void Pin_ReleasedByUsingOnExceptionAndCannotBeAccessedAfterward()
    {
        sql_storage_engine.Exposures.Buffers.BufferFrameExposure frame = new sql_storage_engine.Exposures.Buffers.BufferFrameExposure(new sql_storage_engine.Models.Identifiers.PageId { Value = 8 }, new byte[128]);
        sql_storage_engine.Exposures.Buffers.IPinnedPageExposure? released = null;
        Action operation = () =>
        {
            using var pin = frame.Pin();
            released = pin;
            throw new InvalidOperationException("test failure");
        };
        operation.Should().Throw<InvalidOperationException>();
        frame.ReadPinCount().Should().Be(0);
        ((Func<Memory<byte>>)(() => released!.ReadMemory())).Should().Throw<ObjectDisposedException>();
        ((Action)(() => released!.MarkDirty(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 1 }))).Should().Throw<ObjectDisposedException>();
    }

    [Test]
    public void ConcurrentReleaseOfOneLeaseKeepsOtherLeaseAlive()
    {
        var bytes = new byte[128];
        var frame = new sql_storage_engine.Exposures.Buffers.BufferFrameExposure(new PageId { Value = 9 }, bytes);
        var released = frame.Pin();
        using var active = frame.Pin();
        Parallel.For(0, 128, _ => released.Dispose());
        frame.ReadPinCount().Should().Be(1);
        active.ReadMemory().Span[0] = 42;
        bytes[0].Should().Be(42);
        ((Func<Memory<byte>>)(() => released.ReadMemory())).Should().Throw<ObjectDisposedException>();
        ((Action)(() => ((sql_storage_engine.Exposures.Buffers.BufferFrameExposure)released).Pin())).Should().Throw<ObjectDisposedException>();
    }

    [Test]
    public void ReleasedLeaseCannotReleaseOrDirtyALaterLease()
    {
        var frame = new sql_storage_engine.Exposures.Buffers.BufferFrameExposure(new PageId { Value = 9 }, new byte[128]);
        var released = frame.Pin();
        released.Dispose();
        using var active = frame.Pin();
        released.Dispose();
        frame.ReadPinCount().Should().Be(1);
        ((Action)(() => released.MarkDirty(new LogSequenceNumber { Value = 7 }))).Should().Throw<ObjectDisposedException>();
        frame.ReadIsDirty().Should().BeFalse();
        active.MarkDirty(new LogSequenceNumber { Value = 8 });
        frame.ReadPageLogSequenceNumber().Value.Should().Be(8);
    }
}