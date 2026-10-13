// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Processings.RecoveryPageHeaders;
internal interface IPageHeaderRecoveryPageContextProcessingService
{
    RecoveryPageContext CheckRecoveryPageContext(RecoveryPageContext recoveryPageContext);
}