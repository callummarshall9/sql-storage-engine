using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;

namespace sql_storage_engine.UnitTests;

public sealed class BufferFrameTests
{
    [Test]
    public void Pin_IncrementsAndDisposeDecrementsExactlyOnce()
    {
        var frame = new BufferFrame(new PageId(4), new byte[128]);
        var pin = frame.Pin();
        frame.PinCount.Should().Be(1);
        frame.IsEvictable.Should().BeFalse();

        pin.Dispose();
        pin.Dispose();

        frame.PinCount.Should().Be(0);
        frame.IsEvictable.Should().BeTrue();
    }

    [Test]
    public void MarkDirty_RecordsSuppliedPageLsn()
    {
        var frame = new BufferFrame(new PageId(7), new byte[128]);
        using var pin = frame.Pin();

        pin.MarkDirty(new LogSequenceNumber(99));

        frame.IsDirty.Should().BeTrue();
        frame.PageLogSequenceNumber.Should().Be(new LogSequenceNumber(99));
    }

    [Test]
    public void Pin_ReleasedByUsingOnExceptionAndCannotBeAccessedAfterward()
    {
        var frame = new BufferFrame(new PageId(8), new byte[128]);
        IPinnedPage? released = null;

        Action operation = () =>
        {
            using var pin = frame.Pin();
            released = pin;
            throw new InvalidOperationException("test failure");
        };

        operation.Should().Throw<InvalidOperationException>();
        frame.PinCount.Should().Be(0);
        ((Func<Memory<byte>>)(() => released!.Memory)).Should().Throw<ObjectDisposedException>();
        ((Action)(() => released!.MarkDirty(new LogSequenceNumber(1)))).Should().Throw<ObjectDisposedException>();
    }
}
