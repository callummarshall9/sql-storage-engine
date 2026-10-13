using AwesomeAssertions;
using sql_storage_engine.Exposures.Heap;
using sql_storage_engine.Exposures.Indexes;
using sql_storage_engine.Exposures.Logging;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Heap;

namespace sql_storage_engine.UnitTests;

public sealed class ReferenceArchitectureTests
{
    [Test]
    public async Task RootUpdatesKeepPreviousValueOnCancellationAndValidationFailure()
    {
        var reference = new IndexRootReferenceExposure(new PageId { Value = 1 });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = (await ((Func<Task>)(async () => await reference.UpdateRootAsync(new PageId { Value = 2 }, cancellation.Token)))
            .Should().ThrowAsync<OperationCanceledException>()).Which;
        error.CancellationToken.Should().Be(cancellation.Token);
        reference.ReadRootPageId().Value.Should().Be(1);
        await ((Func<Task>)(async () => await reference.UpdateRootAsync(default))).Should().ThrowAsync<StorageModelValidationException>();
        reference.ReadRootPageId().Value.Should().Be(1);
        await reference.UpdateRootAsync(new PageId { Value = ulong.MaxValue });
        reference.ReadRootPageId().Value.Should().Be(ulong.MaxValue);
    }

    [Test]
    public async Task CheckpointFailureAndCancellationRetainThePriorPublication()
    {
        var reference = new CheckpointReferenceExposure();
        reference.ReadLatestCheckpointLsn().Should().BeNull();
        await reference.PublishAsync(new LogSequenceNumber { Value = 10 });
        reference.SetPublicationFailure(true);
        await ((Func<Task>)(async () => await reference.PublishAsync(new LogSequenceNumber { Value = 20 })))
            .Should().ThrowAsync<IOException>();
        reference.ReadLatestCheckpointLsn()!.Value.Value.Should().Be(10);
        reference.SetPublicationFailure(false);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = (await ((Func<Task>)(async () => await reference.PublishAsync(new LogSequenceNumber { Value = 30 }, cancellation.Token)))
            .Should().ThrowAsync<OperationCanceledException>()).Which;
        error.CancellationToken.Should().Be(cancellation.Token);
        reference.ReadLatestCheckpointLsn()!.Value.Value.Should().Be(10);
    }

    [Test]
    public void MapPreservesCategoryBoundariesAndDeterministicHintOrder()
    {
        var map = new FreeSpaceMapExposure(PageConstants.DefaultSize);
        var first = new PageId { Value = 1 };
        var second = new PageId { Value = 2 };
        map.Update(second,4096);
        map.Update(first,2048);
        map.FindPage(2048).Should().Be(first);
        map.FindPage(2049).Should().Be(second);
        map.Categorize(-1).Should().Be(FreeSpaceCategory.None);
        map.Categorize(2048).Should().Be(FreeSpaceCategory.Tiny);
        map.Categorize(4096).Should().Be(FreeSpaceCategory.Small);
        map.Categorize(6144).Should().Be(FreeSpaceCategory.Medium);
        map.Categorize(6145).Should().Be(FreeSpaceCategory.Medium);
        map.Categorize(6226).Should().Be(FreeSpaceCategory.Large);
        ((Action)(() => map.Update(first,PageConstants.DefaultSize + 1)))
            .Should().Throw<StorageModelValidationException>();
        map.FindPage(2048).Should().Be(first);
        map.Remove(first);
        map.FindPage(2048).Should().Be(second);
        map.Clear();
        map.FindPage(0).Should().BeNull();
    }
}