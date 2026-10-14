// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Services.Foundations.WalRetentions;
internal interface IWalRetentionLogSequenceNumberService
{
    int ReadActiveWalRetentionCount(ulong? registrationId);

    LogSequenceNumber? ReadMinimumRetainedLogSequenceNumber(ulong? registrationId);

    ulong RegisterWalRetention(ulong logSequenceNumber, ulong? registrationId);

    void ReleaseWalRetention(ulong? registrationId);
}