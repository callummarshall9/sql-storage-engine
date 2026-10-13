// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Processings.WalRecordContexts;
internal interface IWalRecordWalAppendContextCodecProcessingService
{
    WalAppendContext WriteWalAppendContext(WalAppendContext walAppendContext);

    WalAppendContext ReadWalAppendContext(WalAppendContext walAppendContext);
}