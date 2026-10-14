// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Foundations.WalRetentions;
internal sealed partial class WalRetentionLogSequenceNumberService
{
    private static void ValidateWalRetentionRegistration(ulong? registrationId)
    {
        if (registrationId == 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(registrationId));
        }
    }

    private static void ValidateWalRetentionSource(ulong logSequenceNumber, ulong? registrationId)
    {
        ValidateWalRetentionRegistration(registrationId: registrationId);

        if (logSequenceNumber == 0)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(logSequenceNumber));
        }
    }

    private void ValidateRegistrationUnderLock(ulong? registrationId)
    {
        if (registrationId.HasValue && !state.Registrations!.ContainsKey(key: registrationId.Value))
        {
            throw new ObjectDisposedException(objectName: "WalRetentionRegistration");
        }
    }
}