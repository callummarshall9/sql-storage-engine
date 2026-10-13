// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Pages;
using sql_storage_engine.Logging;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Foundations.PageHeaders;
using sql_storage_engine.Services.Foundations.WalRecords;

namespace sql_storage_engine.Services.Orchestrations.FuzzCoreRegistries;
internal sealed partial class FuzzCoreRegistryOrchestrationService : IFuzzCoreRegistryOrchestrationService
{
    private readonly PageHeaderService headers = new PageHeaderService();
    private readonly WalRecordService records = new WalRecordService();
    public FuzzCoreRegistry CreateFuzzCoreRegistry(FuzzCoreRegistry fuzzCoreRegistry) =>
        TryCatch(operation: () =>
    {
        ValidateFuzzCoreRegistry(fuzzCoreRegistry: fuzzCoreRegistry);
        var page = new byte[PageConstants.DefaultSize];
        headers.WritePageHeader(destination: page, pageHeader: new PageHeader { PageId = new PageId { Value = 1 }, PageType = PageType.Heap, FormatVersion = new PageFormatVersion { Value = PageHeaderFormat.CurrentVersion }, PageLogSequenceNumber = default, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32, Checksum = 0 });
        headers.WriteChecksum(page: page, pageSize: page.Length);
        var wal = records.WriteWalRecord(walRecord: new WalRecord { Lsn = new LogSequenceNumber { Value = 1 }, PreviousLsn = default, TransactionId = new TransactionId { Value = 1 }, Type = WalRecordType.Commit, Payload = ReadOnlyMemory<byte>.Empty });

        var names = new[]
        {
            "database-header",
            "page-header",
            "heap-slots",
            "rows",
            "keys",
            "catalog-records",
            "overflow-pages",
            "index-pages",
            "wal",
            "backup-manifests"
        };

        byte[][] seeds = [page, page, page, [0, 0, 0, 0], [1], [0, 0, 0, 0], page, page, wal, "{}"u8.ToArray()];
        var targets = new FuzzTarget[names.Length];

        for (var index = 0; index < names.Length; index++)
        {
            targets[index] = new FuzzTarget
            {
                Name = names[index],
                MaximumInputBytes = 16 * 1024 * 1024,
                Decode = fuzzCoreRegistry.Decoders![index],
                GoldenSeeds = new ReadOnlyMemory<byte>[]
                {
                    seeds[index]
                }
            };
        }

        return fuzzCoreRegistry with
        {
            Targets = targets
        };
    });
}