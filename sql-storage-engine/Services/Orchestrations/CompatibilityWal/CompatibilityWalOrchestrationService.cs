// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Processings.CompatibilityWalRecords;
using sql_storage_engine.Services.Processings.WalReadResults;
using sql_storage_engine.Services.Foundations.WalRecords;

namespace sql_storage_engine.Services.Orchestrations.CompatibilityWal;
internal sealed partial class CompatibilityWalOrchestrationService : ICompatibilityWalOrchestrationService
{
    private readonly WalRecordCompatibilityContextProcessingService writer = new WalRecordCompatibilityContextProcessingService();
    private readonly WalRecordReadProcessingService reader = new WalRecordReadProcessingService(walRecords: new WalRecordService());
    public CompatibilityContext ReadCompatibilityContext(CompatibilityContext compatibilityContext) =>
        TryCatch(operation: () =>
    {
        ValidateCompatibilityContext(compatibilityContext: compatibilityContext);
        var encoded = writer.EncodeCompatibilityContext(compatibilityContext: compatibilityContext);

        return compatibilityContext with
        {
            Records = reader.ReadWalReadResult(source: encoded.Bytes!)
            .Records
        };
    });
}