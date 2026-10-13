// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using sql_storage_engine.Models.Access;

namespace sql_storage_engine.Services.Foundations.StorageAccesses;
internal sealed partial class StorageAccessService
{
    private void ValidateStorageAccessEntry(StorageAccess? storageAccess, CancellationToken token, bool reentrant)
    {
        if (storageAccess is not null)
        {
            var accessState = ValidateStorageAccess(storageAccess: storageAccess);

            if (accessState.Disposed)
            {
                throw new ObjectDisposedException(objectName: "StorageAccess");
            }
        }

        token.ThrowIfCancellationRequested();
        ValidateOpenGate();
    }

    private void ValidateOpenGate()
    {
        lock (gate.Sync!)
        {
            ValidateOpenUnderLock();
        }
    }

    private void ValidateOpenUnderLock()
    {
        if (gate.Closed)
        {
            throw new ObjectDisposedException(objectName: "StorageGate");
        }
    }

    private StorageAccessState ValidateStorageAccess(StorageAccess storageAccess)
    {
        ArgumentNullException.ThrowIfNull(argument: storageAccess);
        var state = storageAccess.State;

        if (state is null || !ReferenceEquals(objA: state.Owner, objB: gate.Owner))
        {
            throw new ArgumentException(message: "The storage access handle belongs to another gate.", paramName: nameof(storageAccess));
        }

        return state;
    }

    private StorageAccessState ValidateStorageAccessRegistration(StorageAccess storageAccess, IAsyncDisposable resource)
    {
        var state = ValidateStorageAccess(storageAccess: storageAccess);
        ArgumentNullException.ThrowIfNull(argument: resource);
        ArgumentNullException.ThrowIfNull(argument: state.Scope);
        return state;
    }

    private void ValidateStorageAccessClose(StorageAccess? storageAccess)
    {
        if (storageAccess is not null)
        {
            ValidateStorageAccess(storageAccess: storageAccess);
        }
    }
}