// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Orchestrations.SavepointSessions;
internal sealed partial class StorageSavepointSessionOrchestrationService
{
    private static void ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(argument: name);

        if (name.Length is 0 or > StorageSavepoint.MaximumNameLength)
        {
            throw new ArgumentException("Savepoint names must contain 1 to 32 UTF-16 code units.", nameof(name));
        }
    }

    private static void ValidateSession(StorageSavepointSession storageSavepointSession)
    {
        ArgumentNullException.ThrowIfNull(argument: storageSavepointSession);
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: storageSavepointSession.DatabasePath);
        ArgumentNullException.ThrowIfNull(argument: storageSavepointSession.Runtime);
        ArgumentNullException.ThrowIfNull(argument: storageSavepointSession.Runtime.Entries);
        ArgumentNullException.ThrowIfNull(argument: storageSavepointSession.Flush);
        ArgumentNullException.ThrowIfNull(argument: storageSavepointSession.Restore);
        ArgumentOutOfRangeException.ThrowIfNegative(value: storageSavepointSession.MaximumBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(value: storageSavepointSession.Runtime.Bytes);

        if (storageSavepointSession.Runtime.Entries.Count > StorageSavepoint.MaximumCount)
        {
            throw new ArgumentException(message: "Savepoint count exceeds the supported bound.", paramName: nameof(storageSavepointSession));
        }

        if (storageSavepointSession.Identity.Value == Guid.Empty || storageSavepointSession.Identity.DatabaseId.Value == Guid.Empty)
        {
            throw new ArgumentException(message: "Savepoints require a valid transaction owner.", paramName: nameof(storageSavepointSession));
        }
    }

    private static void ValidateSessionName(StorageSavepointSession storageSavepointSession, string name, CancellationToken token)
    {
        ValidateSession(storageSavepointSession: storageSavepointSession);
        ValidateName(name: name);
        token.ThrowIfCancellationRequested();
    }

    private static void ValidateRollback(StorageSavepointSession storageSavepointSession, CancellationToken token)
    {
        ValidateSession(storageSavepointSession: storageSavepointSession);
        ArgumentNullException.ThrowIfNull(argument: storageSavepointSession.RequestedPoint);
        token.ThrowIfCancellationRequested();
    }
}