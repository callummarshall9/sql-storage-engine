// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Retention;

namespace sql_storage_engine.Services.Foundations.WalRetentions;
internal sealed partial class WalRetentionLogSequenceNumberService : IWalRetentionLogSequenceNumberService
{
    private readonly WalRetentionState state = new WalRetentionState
    {
        Sync = new object (),
        Registrations = new Dictionary<ulong, ulong>()
    };
    public int ReadActiveWalRetentionCount(ulong? registrationId) =>
        TryCatch(operation: () =>
    {
        ValidateWalRetentionRegistration(registrationId: registrationId);

        lock (state.Sync!)
        {
            ValidateRegistrationUnderLock(registrationId: registrationId);
            return state.Registrations!.Count;
        }
    });

    public LogSequenceNumber? ReadMinimumRetainedLogSequenceNumber(ulong? registrationId) =>
        TryCatch<LogSequenceNumber?>(operation: () =>
    {
        ValidateWalRetentionRegistration(registrationId: registrationId);

        lock (state.Sync!)
        {
            ValidateRegistrationUnderLock(registrationId: registrationId);

            return state.Registrations!.Count == 0 ? null : new LogSequenceNumber
            {
                Value = state.Registrations.Values.Min()
            };
        }
    });

    public ulong RegisterWalRetention(ulong logSequenceNumber, ulong? registrationId) =>
        TryCatch(operation: () =>
    {
        ValidateWalRetentionSource(logSequenceNumber: logSequenceNumber, registrationId: registrationId);

        lock (state.Sync!)
        {
            ValidateRegistrationUnderLock(registrationId: registrationId);
            var next = checked(state.NextId + 1);
            state.Registrations!.Add(key: next, value: logSequenceNumber);
            state.NextId = next;
            return next;
        }
    });

    public void ReleaseWalRetention(ulong? registrationId) =>
        TryCatch(operation: () =>
    {
        ValidateWalRetentionRegistration(registrationId: registrationId);

        lock (state.Sync!)
        {
            if (registrationId.HasValue)
            {
                state.Registrations!.Remove(key: registrationId.Value);
            }
        }
    });
}