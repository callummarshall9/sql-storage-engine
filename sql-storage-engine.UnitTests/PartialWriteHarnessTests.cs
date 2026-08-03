using AwesomeAssertions;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class PartialWriteHarnessTests
{
    [TestCase(PartialWritePattern.Prefix)]
    [TestCase(PartialWritePattern.Suffix)]
    [TestCase(PartialWritePattern.Sector)]
    [TestCase(PartialWritePattern.Random)]
    public void TornPagesFailChecksumAndVerifiedImageReconstructsThem(PartialWritePattern pattern)
    {
        var oldPage = Page(1, 0x11); var newPage = Page(1, 0x22);
        var torn = PartialWriteHarness.Tear(oldPage, newPage, pattern);
        ((Action)(() => PageChecksum.ValidateChecksum(torn, torn.Length))).Should().Throw<Exception>();
        PartialWriteHarness.RecoverPage(torn, newPage).Should().Equal(newPage);
    }

    [Test]
    public void CorruptPageWithoutVerifiedSourceHasSpecificError()
    {
        var corrupt = Page(1, 1); corrupt[^1] ^= 1;
        var badSource = Page(1, 2); badSource[^2] ^= 1;
        ((Func<byte[]>)(() => PartialWriteHarness.RecoverPage(corrupt, badSource)))
            .Should().Throw<UnrecoverablePageCorruptionException>();
    }

    [Test]
    public void TornWalTailAndMidLogCorruptionUseDistinctPolicies()
    {
        var first = WalFormat.WriteRecord(new WalRecord(new LogSequenceNumber(1), default,
            new TransactionId(1), WalRecordType.Commit, ReadOnlyMemory<byte>.Empty));
        var second = WalFormat.WriteRecord(new WalRecord(new LogSequenceNumber(41), default,
            new TransactionId(2), WalRecordType.Commit, ReadOnlyMemory<byte>.Empty));
        var complete = first.Concat(second).ToArray();
        PartialWriteHarness.ClassifyWal(complete[..^3]).Should().Be(WalDamagePolicy.TruncateIncompleteTail);
        complete[10] ^= 1;
        PartialWriteHarness.ClassifyWal(complete).Should().Be(WalDamagePolicy.StopForCorruption);
    }

    private static byte[] Page(ulong id, byte value)
    {
        var page = new byte[PageConstants.DefaultSize];
        page.AsSpan(PageHeaderCodec.EncodedLength).Fill(value);
        PageHeaderCodec.Write(page, new PageHeader(new PageId(id), PageType.Heap, PageFormatVersion.Current,
            default, PageChecksumAlgorithm.Crc32, 0));
        PageChecksum.WriteChecksum(page, page.Length); return page;
    }
}
