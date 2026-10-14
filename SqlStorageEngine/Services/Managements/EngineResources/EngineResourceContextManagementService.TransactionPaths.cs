// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Managements.EngineResources;
internal sealed partial class EngineResourceContextManagementService
{
    public EngineResourceContext ReadTransactionJournalPathEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return durability.ReadTransactionJournalPathEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ReadTransactionActivePathEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return durability.ReadTransactionActivePathEngineResourceContext(engineResourceContext: engineResourceContext);
    });
}