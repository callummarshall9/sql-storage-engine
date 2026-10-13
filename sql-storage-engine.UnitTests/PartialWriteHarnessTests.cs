// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Exposures.Diagnostics;
using sql_storage_engine.Exposures.Logging;
using AwesomeAssertions;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class PartialWriteHarnessTests
{
    [TestCase(PartialWritePattern.Prefix)]
    [TestCase(PartialWritePattern.Suffix)]
    [TestCase(PartialWritePattern.Sector)]
    [TestCase(PartialWritePattern.Random)]
    public void TornPagesFailChecksumAndVerifiedImageReconstructsThem(PartialWritePattern pattern)
    {
        var oldPage = Page(1,0x11);
        var newPage = Page(1,0x22);
        var torn = PartialWriteExposure.Tear(oldPage,newPage,pattern);
        ((Action)(() => PageChecksumExposure.ValidateChecksum(torn,torn.Length))).Should().Throw<Exception>();
        WriteDamageExposure.RecoverPage(torn,newPage).Should().Equal(newPage);
    }

    [Test]
    public void CorruptPageWithoutVerifiedSourceHasSpecificError()
    {
        var corrupt = Page(1,1);
        corrupt[^1] ^= 1;
        var badSource = Page(1,2);
        badSource[^2] ^= 1;
        ((Func<byte[]>)(() => WriteDamageExposure.RecoverPage(corrupt,badSource))).Should().Throw<UnrecoverablePageCorruptionException>();
    }

    [Test]
    public void TornWalTailAndMidLogCorruptionUseDistinctPolicies()
    {
        var first = WalRecordExposure.WriteWalRecord(new WalRecord { Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 1 }, PreviousLsn = default, TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 }, Type = WalRecordType.Commit, Payload = ReadOnlyMemory<byte>.Empty });
        var second = WalRecordExposure.WriteWalRecord(new WalRecord { Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 41 }, PreviousLsn = default, TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId { Value = 2 }, Type = WalRecordType.Commit, Payload = ReadOnlyMemory<byte>.Empty });
        var complete = first.Concat(second).ToArray();
        WriteDamageExposure.ClassifyWal(complete[..^3]).Should().Be(WalDamagePolicy.TruncateIncompleteTail);
        complete[10] ^= 1;
        WriteDamageExposure.ClassifyWal(complete).Should().Be(WalDamagePolicy.StopForCorruption);
    }

    private static byte[] Page(ulong id, byte value)
    {
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        page.AsSpan(sql_storage_engine.Models.Pages.PageHeaderFormat.EncodedLength).Fill(value);
        sql_storage_engine.Exposures.Pages.PageHeaderExposure.WritePageHeader(page,new PageHeader { PageId = new sql_storage_engine.Models.Identifiers.PageId { Value = id }, PageType = PageType.Heap, FormatVersion = new sql_storage_engine.Models.Pages.PageFormatVersion { Value = sql_storage_engine.Models.Pages.PageHeaderFormat.CurrentVersion }, PageLogSequenceNumber = default, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32, Checksum = 0 });
        PageChecksumExposure.WriteChecksum(page,page.Length);
        return page;
    }
}