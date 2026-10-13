// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Processings.PageRecoveries;
internal interface IPageHeaderRecoveryProcessingService
{
    byte[] RecoverPage(ReadOnlySpan<byte> page, ReadOnlySpan<byte> verifiedFullPageImage);
}