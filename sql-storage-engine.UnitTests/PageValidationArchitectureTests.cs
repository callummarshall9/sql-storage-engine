using AwesomeAssertions;
using NUnit.Framework;
using sql_storage_engine.Exposures.Diagnostics;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class PageValidationArchitectureTests
{
    [Test]
    public void HeaderIdentityValidationRunsBeforeDestinationMutation()
    {
        var destination = Enumerable.Repeat((byte)0xCC, 32).ToArray();
        var invalid = new PageHeader
        {
            PageId = new PageId { Value = 1 },
            PageType = Pages.PageType.Heap,
            FormatVersion = new PageFormatVersion { Value = 2 },
            ChecksumAlgorithm = Pages.PageChecksumAlgorithm.Crc32
        };
        ((Action)(() => PageHeaderExposure.WritePageHeader(destination, invalid)))
            .Should().Throw<StorageFormatException>();
        destination.Should().OnlyContain(value => value == 0xCC);
    }

    [Test]
    public void MetricsRejectInvalidDurationsAndOperationsAfterClosing()
    {
        using var metrics = new StorageMetricsExposure();
        ((Action)(() => metrics.RecordPageFlush(TimeSpan.FromTicks(-1), true)))
            .Should().Throw<StorageModelValidationException>();
        metrics.Dispose();
        metrics.Dispose();
        ((Action)(() => metrics.RecordPageRead(true)))
            .Should().Throw<StorageModelValidationException>();
    }
}