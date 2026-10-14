// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Managements.EngineResources;
internal partial interface IEngineResourceContextManagementService
{
    EngineResourceContext ReadTransactionJournalPathEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext ReadTransactionActivePathEngineResourceContext(EngineResourceContext engineResourceContext);
}